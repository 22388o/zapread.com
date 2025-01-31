﻿using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Entity;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using zapread.com.Database;
using zapread.com.Models.API.DataTables;
using zapread.com.Models.Database;
using System.Globalization;
using zapread.com.Models.API.Groups;
using System.Web.Http;
using zapread.com.Helpers;

namespace zapread.com.API
{
    /// <summary>
    /// This controller should support a front end using https://imballinst.github.io/react-bs-datatable/?path=/story/advanced-guides--asynchronous-table
    /// </summary>
    public class GroupsController : ApiController
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataTableParameters"></param>
        /// <returns></returns>
        [AcceptVerbs("POST")]
        [Route("api/v1/groups/list")]
        public async Task<ListGroupsResponse> List(DataTableParameters dataTableParameters)
        {
            if (dataTableParameters == null)
            {
                //Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return new ListGroupsResponse() { success = false, message = "No query provided" };
            }

            using (var db = new ZapContext())
            {
                var search = dataTableParameters.Search;

                // We need to know the user making the call (if any) so we know if they are a member of the group or not
                User user = await GetCurrentUser(db).ConfigureAwait(true);
                int userid = user != null ? user.Id : 0;

                // Build query
                var groupsQ = db.Groups
                    .Select(g => new
                    {
                        numPosts = g.Posts.Count,
                        numMembers = g.Members.Count,
                        IsMember = g.Members.Select(m => m.Id).Contains(userid),
                        IsModerator = g.Moderators.Select(m => m.Id).Contains(userid),
                        IsAdmin = g.Administrators.Select(m => m.Id).Contains(userid),
                        g.GroupId,
                        g.GroupName,
                        g.Tags,
                        g.TotalEarned,
                        g.TotalEarnedToDistribute,
                        g.CreationDate,
                        Icon = g.GroupImage == null ? g.Icon : null, // Only if GroupImage doesn't exist
                        g.Tier,
                        IconId = g.GroupImage == null ? 0 : g.GroupImage.ImageId
                    }).AsNoTracking();

                if (search != null && search.Value != null)
                {
                    groupsQ = groupsQ.Where(g => g.GroupName.Contains(search.Value) || g.Tags.Contains(search.Value));
                }

                groupsQ = groupsQ.OrderByDescending(g => g.TotalEarned + g.TotalEarnedToDistribute);

                var groups = await groupsQ
                    .Skip(dataTableParameters.Start)
                    .Take(dataTableParameters.Length)
                    .ToListAsync().ConfigureAwait(false);

                var values = groups.Select(g => new GroupInfo()
                {
                    Id = g.GroupId,
                    CreatedddMMMYYYY = g.CreationDate == null ? "2 Aug 2018" : g.CreationDate.Value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
                    Name = g.GroupName,
                    NumMembers = g.numMembers,
                    NumPosts = g.numPosts,
                    Tags = g.Tags != null ? g.Tags.Split(',').ToList() : new List<string>(),
                    Icon = g.Icon != null ? "fa-" + g.Icon : null, // "fa-bolt",  // NOTE: this is legacy, and will eventually be replaced.  All new groups will have image icons.
                    Level = g.Tier,
                    Progress = GetGroupProgress(g.TotalEarned, g.TotalEarnedToDistribute, g.Tier),
                    IsMember = g.IsMember,
                    IsLoggedIn = user != null,
                    IsMod = g.IsModerator,
                    IsAdmin = g.IsAdmin,
                }).ToList();

                var ret = new ListGroupsResponse()
                {
                    success = true,
                    draw = dataTableParameters.Draw,
                    recordsTotal = await groupsQ.CountAsync().ConfigureAwait(false),
                    //await db.Groups.CountAsync().ConfigureAwait(false),
                    recordsFiltered = await groupsQ.CountAsync().ConfigureAwait(false),
                    data = values
                };

                return ret;
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        [AcceptVerbs("POST")]
        [Route("api/v1/groups/checkexists")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>")]
        public async Task<IHttpActionResult> CheckExists(CheckExistsGroupParameters p)
        {
            if (p == null)
            {
                return BadRequest();
            }

            using (var db = new ZapContext())
            {
                var groupIdCheck = -1;

                if (p.GroupId.HasValue)
                {
                    groupIdCheck = p.GroupId.Value;
                }
                else
                {
                    return BadRequest();
                }

                bool exists = await GroupExists(p.GroupName.CleanUnicode(), groupIdCheck, db).ConfigureAwait(true);
                return Ok(new CheckExistsGroupResponse() { exists = exists, success = true });
            }
        }

        private static async Task<bool> GroupExists(string GroupName, int groupId, ZapContext db)
        {
            Group matched = await db.Groups.Where(g => g.GroupName == GroupName).FirstOrDefaultAsync().ConfigureAwait(true);
            if (matched != null)
            {
                if (matched.GroupId != groupId)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Add a new group
        /// </summary>
        /// <param name="newGroup"></param>
        /// <returns>AddGroupResponse</returns>
        [AcceptVerbs("POST")]
        [Route("api/v1/groups/add")]
        public async Task<AddGroupResponse> Add(AddGroupParameters newGroup)
        {
            if (newGroup == null)
            {
                // use this to return status code
                // https://www.tutorialsteacher.com/webapi/action-method-return-type-in-web-api
                return new AddGroupResponse()
                {
                    success = false
                };
            }

            using (var db = new ZapContext())
            {
                // Ensure not a duplicate group!
                var cleanName = newGroup.GroupName.CleanUnicode();
                bool exists = await GroupExists(cleanName, -1, db).ConfigureAwait(true);
                if (exists)
                {
                    return new AddGroupResponse() { success = false };
                }

                var user = await GetCurrentUser(db).ConfigureAwait(true);
                var icon = await GetGroupIcon(newGroup.ImageId, db).ConfigureAwait(true);

                Group g = new Group()
                {
                    GroupName = cleanName,
                    TotalEarned = 0.0,
                    TotalEarnedToDistribute = 0.0,
                    Moderators = new List<User>(),
                    Members = new List<User>(),
                    Administrators = new List<User>(),
                    Tags = newGroup.Tags,
                    Icon = null, //m.Icon,  // This field is now depricated - will be removed
                    GroupImage = icon,
                    CreationDate = DateTime.UtcNow,
                    DefaultLanguage = newGroup.Language == null ? "en" : newGroup.Language, // Ensure value
                };

                g.Members.Add(user);
                g.Moderators.Add(user);
                g.Administrators.Add(user);

                db.Groups.Add(g);
                await db.SaveChangesAsync().ConfigureAwait(true);

                return new AddGroupResponse()
                {
                    success = true,
                    GroupId = g.GroupId
                };
            }
        }

        /// <summary>
        /// Get posts from the group
        /// </summary>
        /// <param name="req">query parameters</param>
        /// <returns></returns>
        [AcceptVerbs("POST")]
        [Route("api/v1/groups/posts")]
        public async Task<IHttpActionResult> GetPosts(GetGroupPostsParameters req)
        {
            if (req == null)
            {
                return BadRequest();
            }

            if (req.groupId < 1)
            {
                return BadRequest();
            }

            int BlockSize = req.blockSize ?? 10;

            int BlockNumber = req.blockNumber ?? 0;

            var userAppId = User.Identity.GetUserId();

            using (var db = new ZapContext())
            {
                var user = await db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.AppId == userAppId).ConfigureAwait(true);

                int userId = user == null ? 0 : user.Id;

                IQueryable<Post> validposts = QueryHelpers.QueryValidPosts(userId, null, db,
                    user: null);

                var groupPosts = QueryHelpers.OrderPostsByNew(validposts, req.groupId, true);

                var response = new GetGroupPostsResponse()
                {
                    HasMorePosts = groupPosts.Count() >= 10,
                    Posts = await QueryHelpers.QueryPostsVm(start: BlockNumber*BlockSize, count: BlockSize, postquery: groupPosts, user: user, userId: userId).ConfigureAwait(true),
                    success = true,
                };

                return Ok(response);
            }
        }

        /// <summary>
        /// Loads the information on a specified group
        /// </summary>
        /// <param name="groupInfo"></param>
        /// <returns>LoadGroupResponse</returns>
        [AcceptVerbs("POST")]
        [Route("api/v1/groups/load")]
        public async Task<IHttpActionResult> Load(LoadGroupParameters groupInfo)
        {
            // validate
            if (groupInfo == null)
            {
                return BadRequest();
            }

            if (groupInfo.groupId == 0)
            {
                return BadRequest();
            }

            using (var db = new ZapContext())
            {
                var userAppId = User.Identity.GetUserId();            // Get the logged in user ID
                int userId = 0;
                var groupId = groupInfo.groupId;
                var isIgnoring = false;
                if (userAppId != null)
                {
                    var userInfo = await db.Users
                        .Where(u => u.AppId == userAppId)
                        .Select(u => new { 
                            u.Id,
                            IsIgnored = u.IgnoredGroups.Select(gr => gr.GroupId).Contains(groupId),
                        }).FirstOrDefaultAsync().ConfigureAwait(true);

                    userId = userInfo.Id;
                    isIgnoring = userInfo.IsIgnored;
                }

                var reqGroupQ = await db.Groups
                    .Where(g => g.GroupId == groupInfo.groupId)
                    .Select(g => new
                    {
                        g.GroupId,
                        g.DefaultLanguage,
                        g.GroupName,
                        ImageId = g.GroupImage == null ? 0 : g.GroupImage.ImageId,
                        g.Tags,
                        g.ShortDescription,
                        g.Tier,
                        Earned = g.TotalEarned + g.TotalEarnedToDistribute,
                        NumMembers = g.Members.Count,
                        NumPosts = g.Posts.Count,
                        IsMember = g.Members.Select(m => m.Id).Contains(userId),
                        IsModerator = g.Moderators.Select(m => m.Id).Contains(userId),
                        IsAdmin = g.Administrators.Select(m => m.Id).Contains(userId),
                    }).FirstOrDefaultAsync().ConfigureAwait(true);

                if (reqGroupQ == null)
                {
                    return NotFound();
                }

                // Convert to GroupInfo object for return
                var reqGroup = new GroupInfo()
                {
                    Id = reqGroupQ.GroupId,
                    DefaultLanguage = reqGroupQ.DefaultLanguage == null ? "en" : reqGroupQ.DefaultLanguage,
                    Name = reqGroupQ.GroupName,
                    IconId = reqGroupQ.ImageId,
                    Tags = reqGroupQ.Tags != null ? reqGroupQ.Tags.Split(',').ToList() : new List<string>(),
                    ShortDescription = reqGroupQ.ShortDescription,
                    NumMembers = reqGroupQ.NumMembers,
                    IsAdmin = reqGroupQ.IsAdmin,
                    IsMod = reqGroupQ.IsModerator,
                    IsMember = reqGroupQ.IsMember,
                    IsIgnoring = isIgnoring,
                    Level = reqGroupQ.Tier,
                    Earned = Convert.ToUInt64(reqGroupQ.Earned),
                };

                return Ok(new LoadGroupResponse() { 
                    success = true, 
                    group = reqGroup, 
                    IsLoggedIn = User.Identity.GetUserId() != null, 
                    UserName = User.Identity.Name
                });
            }
        }

        /// <summary>
        /// Update a group parameters (Admin action)
        /// </summary>
        /// <param name="existingGroup"></param>
        /// <returns></returns>
        [AcceptVerbs("PUT")]
        [Route("api/v1/groups/update")]
        public async Task<IHttpActionResult> Update(UpdateGroupParameters existingGroup)
        {
            // validate
            if (existingGroup == null)
            {
                return BadRequest();
                //throw new ArgumentNullException(nameof(groupInfo));
            }

            using (var db = new ZapContext())
            {
                var user = await GetCurrentUser(db).ConfigureAwait(true);

                var group = await db.Groups
                    .Where(g => g.GroupId == existingGroup.GroupId)
                    .Include(gr => gr.Administrators)
                    .FirstOrDefaultAsync().ConfigureAwait(true);

                if (group == null)
                {
                    return BadRequest(Properties.Resources.ErrorGroupNotFound);
                }

                if (!group.Administrators.Select(a => a.AppId).Contains(user.AppId))
                {
                    return Unauthorized();
                }

                var cleanName = existingGroup.GroupName.CleanUnicode();
                bool exists = await GroupExists(cleanName, existingGroup.GroupId, db).ConfigureAwait(true);

                if (exists)
                {
                    return BadRequest(Properties.Resources.ErrorGroupDuplicate);
                }

                // Make updates
                var icon = await GetGroupIcon(existingGroup.ImageId, db).ConfigureAwait(true);

                if (icon == null)
                {
                    return BadRequest(Properties.Resources.ErrorIconNotFound);
                }

                group.DefaultLanguage = existingGroup.Language == null ? "en" : existingGroup.Language; // Ensure value
                group.Tags = existingGroup.Tags;
                
                group.GroupName = cleanName;
                group.GroupImage = icon;

                await db.SaveChangesAsync().ConfigureAwait(true);

                return Ok(new AddGroupResponse()
                {
                    success = true,
                    GroupId = group.GroupId
                });
            }
        }

        private static async Task<Models.UserImage> GetGroupIcon(int ImageId, ZapContext db)
        {
            var icon = await db.Images.Where(im => im.ImageId == ImageId).FirstOrDefaultAsync().ConfigureAwait(true);

            if (icon == null || icon.Image == null)
            {
                // Image 1 is usually the default
                icon = await db.Images.Where(im => im.ImageId == 1).FirstOrDefaultAsync().ConfigureAwait(true);
                if (icon == null || icon.Image == null)
                {
                    // Get any icon
                    icon = await db.Images.Where(im => im.Image != null).FirstOrDefaultAsync().ConfigureAwait(true);
                }
            }

            return icon;
        }

        private async Task<User> GetCurrentUser(ZapContext db)
        {
            var userId = User.Identity.GetUserId();
            var user = await db.Users
                .Include(u => u.Settings)
                .FirstOrDefaultAsync(u => u.AppId == userId).ConfigureAwait(true);
            return user;
        }

        /// <summary>
        /// Returns the progress of the group to get to the next tier
        /// </summary>
        /// <param name="TotalEarned"></param>
        /// <param name="TotalEarnedToDistribute"></param>
        /// <param name="Tier"></param>
        /// <returns></returns>
        protected static int GetGroupProgress(double TotalEarned, double TotalEarnedToDistribute, double Tier)
        {
            var e = TotalEarned + TotalEarnedToDistribute;
            //var level = GetGroupLevel(g);

            if (Tier == 0)
            {
                return Convert.ToInt32(100.0 * e / 1000.0);
            }
            if (Tier == 1)
            {
                return Convert.ToInt32(100.0 * (e - 1000.0) / 10000.0);
            }
            if (Tier == 2)
            {
                return Convert.ToInt32(100.0 * (e - 10000.0) / 50000.0);
            }
            if (Tier == 3)
            {
                return Convert.ToInt32(100.0 * (e - 50000.0) / 200000.0);
            }
            if (Tier == 4)
            {
                return Convert.ToInt32(100.0 * (e - 200000.0) / 500000.0);
            }
            if (Tier == 5)
            {
                return Convert.ToInt32(100.0 * (e - 500000.0) / 1000000.0);
            }
            if (Tier == 6)
            {
                return Convert.ToInt32(100.0 * (e - 1000000.0) / 5000000.0);
            }
            if (Tier == 7)
            {
                return Convert.ToInt32(100.0 * (e - 5000000.0) / 10000000.0);
            }
            if (Tier == 8)
            {
                return Convert.ToInt32(100.0 * (e - 10000000.0) / 20000000.0);
            }
            if (Tier == 9)
            {
                return Convert.ToInt32(100.0 * (e - 20000000.0) / 50000000.0);
            }
            return 100;
        }
    }
}