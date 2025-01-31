﻿using Hangfire;
using HtmlAgilityPack;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using MvcSiteMapProvider;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using zapread.com.Database;
using zapread.com.Helpers;
using zapread.com.Models;
using zapread.com.Models.API.Account;
using zapread.com.Models.API.DataTables;
using zapread.com.Models.Database;
using zapread.com.Models.Database.Financial;
using zapread.com.Services;

namespace zapread.com.Controllers
{
    //[RoutePrefix("{Type:regex(Post|post)}")]
    /// <summary>
    /// Controller for the /Post/ Route
    /// </summary>
    public class PostController : Controller
    {
        private ApplicationRoleManager _roleManager;
        private ApplicationUserManager _userManager;

        /// <summary>
        /// Access for Owin user manager
        /// </summary>
        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        /// <summary>
        /// Access for Owin roles manager
        /// </summary>
        public ApplicationRoleManager RoleManager
        {
            get
            {
                return _roleManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationRoleManager>();
            }
            private set
            {
                _roleManager = value;
            }
        }
        
        /// <summary>
        /// Fetch a draft post (by post ID)
        /// </summary>
        /// <param name="postId"></param>
        /// <returns></returns>
        [Route("Post/Draft/Load")]
        [HttpPost]
        [ValidateJsonAntiForgeryToken]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA3147:Mark Verb Handlers With Validate Antiforgery Token", Justification = "token in header")]
        public async Task<ActionResult> GetDraft(int postId, bool isDraft = true)
        {
            var userId = User.Identity.GetUserId();

            if (userId == null)
            {
                Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return Json(new { success = false, message = "Credentials failure" });
            }
            using (var db = new ZapContext())
            {
                var draftPost = await db.Posts
                    .Where(p => p.UserId.AppId == userId)
                    .Where(p => p.IsDraft == isDraft)
                    .Where(p => p.IsDeleted == false)
                    .Where(p => p.PostId == postId)
                    .Select(p => new { 
                        p.PostId,
                        p.Group.GroupId,
                        p.PostTitle,
                        p.Group.GroupName,
                        p.Content
                    })
                    .FirstOrDefaultAsync().ConfigureAwait(true);

                if (draftPost == null)
                {
                    Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return Json(new { success = false, message = "Draft post not found." });
                }

                return Json(new { success = true, draftPost });
            }
        }

        /// <summary>
        /// Delete a draft post
        /// </summary>
        /// <param name="postId"></param>
        /// <returns></returns>
        [Route("Post/Draft/Delete")]
        [HttpPost]
        [ValidateJsonAntiForgeryToken]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA3147:Mark Verb Handlers With Validate Antiforgery Token", Justification = "token in header")]
        public async Task<ActionResult> DeleteDraft(int postId)
        {
            var userId = User.Identity.GetUserId();
            using (var db = new ZapContext())
            {
                var post = await db.Posts
                    .FirstOrDefaultAsync(p => p.PostId == postId).ConfigureAwait(false);

                if (!User.IsInRole("Administrator"))
                {
                    if (post.UserId.AppId != userId)
                    {
                        return RedirectToAction("Index", "Home");
                    }
                }

                post.IsDeleted = true;
                await db.SaveChangesAsync().ConfigureAwait(false);

                return Json(new { success = true });
            }
        }

        /// <summary>
        /// This method returns the drafts table on the post editing view.
        /// </summary>
        /// <param name="dataTableParameters"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateJsonAntiForgeryToken]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA3147:Mark Verb Handlers With Validate Antiforgery Token", Justification = "token in header")]
        public async Task<ActionResult> GetDrafts(DataTableParameters dataTableParameters)
        {
            if (dataTableParameters == null)
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Json(new { success = false, message = "No parameters passed to method call." });
            }

            var userId = User.Identity.GetUserId();

            if (userId == null)
            {
                Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return Json(new { success = false, message = "Credentials failure" });
            }

            using (var db = new ZapContext())
            {
                User u = db.Users
                        .Where(us => us.AppId == userId).First();

                var draftPostsQuery = db.Posts
                    .Where(p => p.UserId.Id == u.Id)
                    .Where(p => p.IsDraft == true)
                    .Where(p => p.IsDeleted == false);

                var drafts = await draftPostsQuery
                    .OrderByDescending(p => p.TimeStamp)
                    .Skip(dataTableParameters.Start)
                    .Take(dataTableParameters.Length)
                    .Select(t => new
                    {
                        t.TimeStamp,//.HasValue ? t.TimeStamp.Value.ToString("yyyy-MM-dd HH:mm:ss") : "",
                        t.PostTitle,
                        t.Group.GroupName,
                        t.Group.GroupId,
                        t.PostId,
                    })
                    .ToListAsync().ConfigureAwait(true);

                int numrec = await draftPostsQuery
                    .CountAsync().ConfigureAwait(true);

                var values = drafts.Select(t => new
                {
                    TimeStamp = t.TimeStamp.HasValue ? t.TimeStamp.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : "",
                    t.PostTitle,
                    t.GroupName,
                    t.GroupId,
                    t.PostId,
                });

                var ret = new
                {
                    draw = dataTableParameters.Draw,
                    recordsTotal = numrec,
                    recordsFiltered = numrec,
                    data = values
                };
                return Json(ret);
            }
        }

        /// <summary>
        /// Gets and updates post impressions count.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet, Route("Post/Impressions/{id}")]
        public async Task<PartialViewResult> Impressions(int? id)
        {
            using (var db = new ZapContext())
            {
                var post = await db.Posts
                    .FirstOrDefaultAsync(p => p.PostId == id).ConfigureAwait(false);
                if (post != null)
                {
                    post.Impressions += 1;
                    ViewBag.PostImpressions = post.Impressions;
                    await db.SaveChangesAsync().ConfigureAwait(false);
                }
                return PartialView("_Impressions");
            }
        }

        /// <summary>
        /// Only Admin or Mod user can make a post sticky in the group
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost, ValidateJsonAntiForgeryToken]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA3147:Mark Verb Handlers With Validate Antiforgery Token", Justification = "<Pending>")]
        public async Task<JsonResult> ToggleStickyPost(int id)
        {
            var userId = User.Identity.GetUserId();
            using (var db = new ZapContext())
            {
                var post = db.Posts
                    .Include(p => p.UserId)
                    .Include(p => p.UserId.ProfileImage)
                    .FirstOrDefault(p => p.PostId == id);

                if (post == null)
                {
                    return Json(new { Result = "Error" }, JsonRequestBehavior.AllowGet);
                }

                if (post.UserId.AppId == userId || UserManager.IsInRole(userId, "Administrator") || post.UserId.GroupModeration.Select(g => g.GroupId).Contains(post.Group.GroupId))
                {
                    post.IsSticky = !post.IsSticky;

                    await db.SaveChangesAsync().ConfigureAwait(false);
                    return Json(new { Result = "Success" }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { Result = "Error" }, JsonRequestBehavior.AllowGet);
                }
            }
        }

        /// <summary>
        /// Admin or Mod can toggle a post as NSFW
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateJsonAntiForgeryToken]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA3147:Mark Verb Handlers With Validate Antiforgery Token", Justification = "<Pending>")]
        public async Task<JsonResult> ToggleNSFW(int id)
        {
            var userId = User.Identity.GetUserId();

            if (userId == null)
            {
                Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return Json(new { success = false, message = "Credentials failure" }, JsonRequestBehavior.AllowGet);
            }

            using (var db = new ZapContext())
            {
                var post = await db.Posts
                    .Include(p => p.UserId)
                    .FirstOrDefaultAsync(p => p.PostId == id).ConfigureAwait(false);

                if (post == null)
                {
                    Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return Json(new { success = false, message = "Invalid post" }, JsonRequestBehavior.AllowGet);
                }

                var callingUserIsMod = await db.Users
                    .Where(u => u.AppId == userId)
                    .SelectMany(u => u.GroupModeration.Select(g => g.GroupId))
                    .ContainsAsync(post.Group.GroupId).ConfigureAwait(false);

                if (post.UserId.AppId == userId 
                    || UserManager.IsInRole(userId, "Administrator") 
                    || callingUserIsMod)
                {
                    post.IsNSFW = !post.IsNSFW;

                    // Alert the post owner
                    var postOwner = post.UserId;

                    // Add Alert
                    var alert = new UserAlert()
                    {
                        TimeStamp = DateTime.Now,
                        Title = (post.IsNSFW ? "Your post has been marked NSFW : " : "Your post is no longer marked NSFW : ") + post.PostTitle,
                        Content = "A moderator has changed the Not Safe For Work status of your post.",
                        IsDeleted = false,
                        IsRead = false,
                        To = postOwner,
                        PostLink = post,
                    };
                    postOwner.Alerts.Add(alert);
                    await db.SaveChangesAsync().ConfigureAwait(false);
                    return Json(new { success=true, message = "Success", post.IsNSFW }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    return Json(new { message = "Credentials failure" }, JsonRequestBehavior.AllowGet);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="postId"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("Post/Edit/{postId?}")]
        public async Task<ActionResult> Edit(int? postId, int? group)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Request.Url.ToString() });
            }

            var userId = User.Identity.GetUserId();

            using (var db = new ZapContext())
            {
                //await EnsureUserExists(userId, db).ConfigureAwait(true);
                //var user = db.Users.Where(u => u.AppId == userId).First();
                //var communityGroup = db.Groups.FirstOrDefault(g => g.GroupId == 1);
                //var postGroup = db.Groups.FirstOrDefault(g => g.GroupId == group);
                //var post = new Post()
                //{
                //    Content = "",
                //    UserId = user,
                //    Group = postGroup,// ?? communityGroup,
                //    Language = (postGroup ?? communityGroup).DefaultLanguage ?? "en",
                //};

                // List of languages known
                //var languages = CultureInfo.GetCultures(CultureTypes.NeutralCultures).Skip(1)
                //    .GroupBy(ci => ci.TwoLetterISOLanguageName)
                //    .Select(g => g.First())
                //    .Select(ci => ci.Name + ":" + ci.NativeName).ToList();

                //var vm = new NewPostViewModel()
                //{
                //    Post = post,
                //    Languages = languages,
                //};

                return View();
            }
        }

        private async Task EnsureUserExists(string userId, ZapContext db)
        {
            if (userId != null)
            {
                if (!db.Users.Where(u => u.AppId == userId).Any())
                {
                    // no user entry
                    User u = new User()
                    {
                        AboutMe = "Nothing to tell.",
                        AppId = userId,
                        Name = User.Identity.Name,
                        ProfileImage = new UserImage(),
                        ThumbImage = new UserImage(),
                        Funds = new UserFunds(),
                        Settings = new UserSettings(),
                        DateJoined = DateTime.UtcNow,
                    };
                    db.Users.Add(u);
                    await db.SaveChangesAsync();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="postId"></param>
        /// <param name="groupId"></param>
        /// <param name="content"></param>
        /// <param name="postTitle"></param>
        /// <param name="isDraft"></param>
        /// <param name="isNSFW"></param>
        /// <param name="postQuietly"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        [Route("Post/Submit")]
        [HttpPost]
        [ValidateJsonAntiForgeryToken]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA3147:Mark Verb Handlers With Validate Antiforgery Token", Justification = "Token in JSON header")]
        public async Task<ActionResult> Submit(int postId, int groupId, string content, string postTitle, bool isDraft, bool isNSFW, bool postQuietly, string language)
        {
            var userId = User.Identity.GetUserId();

            if (userId == null)
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return Json(new { success = false, message = "Error finding user account." });
            }

            using (var db = new ZapContext())
            {
                var user = await db.Users.Where(u => u.AppId == userId)
                    .FirstOrDefaultAsync().ConfigureAwait(true);  // Note ConfigureAwait must be true since we need to preserve context for the mailer

                // Cleanup post HTML
                string contentStr = CleanContent(content);

                var postGroup = await db.Groups.FirstOrDefaultAsync(g => g.GroupId == groupId).ConfigureAwait(true);

                string postLanguage = LanguageHelpers.NameToISO(language);

                Post post = null;
                if (postId > 0)
                {
                    // Updated post
                    post = await db.Posts
                        .Include(pst => pst.UserId)
                        .Where(pst => pst.PostId == postId)
                        .FirstOrDefaultAsync().ConfigureAwait(true);

                    // Ensure user owns this post (or is site admin)
                    if (post.UserId.Id != user.Id && !User.IsInRole("Administrator"))
                    {
                        // Editing another user's post.
                        Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        return Json(new { result = "failure", success = false, message = "User mismatch" });
                    }

                    post.PostTitle = postTitle == null ? "Post" : postTitle.CleanUnicode().SanitizeXSS();
                    post.Group = postGroup;
                    post.Content = contentStr;
                    post.Language = postLanguage ?? post.Language;
                    post.IsNSFW = isNSFW;

                    if (post.IsDraft) // Post was or is draft - set timestamp.
                    {
                        post.TimeStamp = DateTime.UtcNow;
                    }
                    else // Post has been published, don't update timestamp, update edit timestamp.
                    {
                        post.TimeStampEdited = DateTime.UtcNow;
                    }

                    if (post.IsDraft && !isDraft) // Post was a draft, now published
                    {
                        post.IsDraft = isDraft;
                        await db.SaveChangesAsync().ConfigureAwait(true);
                        // We don't return yet - so notifications can be fired off.
                    }
                    else
                    {
                        post.IsDraft = isDraft;
                        await db.SaveChangesAsync().ConfigureAwait(true);

                        if (!isDraft && !postQuietly && !post.TimeStampEdited.HasValue)
                        {
                            // Send alerts to users subscribed to users
                            try
                            {
                                var mailer = DependencyResolver.Current.GetService<MailerController>();
                                await AlertUsersNewPost(db, user, post, mailer).ConfigureAwait(true);
                            }
                            catch (Exception e)
                            {
                                // noted.
                            }
                        }

                        return Json(new { result = "success", success = true, postId = post.PostId, HTMLContent = contentStr });
                    }
                }
                else
                {
                    // New post
                    post = new Post()
                    {
                        Content = contentStr,
                        UserId = user,
                        TotalEarned = 0,
                        IsDeleted = false,
                        Score = 1,
                        Group = postGroup,
                        TimeStamp = DateTime.UtcNow,
                        VotesUp = new List<User>() { user },
                        PostTitle = postTitle == null ? "" : postTitle.CleanUnicode().SanitizeXSS(),
                        IsDraft = isDraft,
                        Language = postLanguage,
                        IsNSFW = isNSFW,
                    };

                    db.Posts.Add(post);
                    await db.SaveChangesAsync().ConfigureAwait(true);

                    if (!isDraft && !postQuietly)
                    {
                        // Send alerts to users subscribed to users
                        try
                        {
                            var mailer = DependencyResolver.Current.GetService<MailerController>();
                            await AlertUsersNewPost(db, user, post, mailer).ConfigureAwait(true);
                        }
                        catch (Exception e)
                        {
                            // noted.
                        }
                    }
                }

                return Json(new { success = true, postId = post.PostId });
            }
        }

        private static string CleanContent(string content)
        {
            HtmlDocument postDocument = new HtmlDocument();
            postDocument.LoadHtml(content);

            // Check links
            var postLinks = postDocument.DocumentNode.SelectNodes("//a/@href");
            if (postLinks != null)
            {
                foreach (var link in postLinks.ToList())
                {
                    string url = link.GetAttributeValue("href", "");
                    // replace links to embedded videos
                    if (url.Contains("youtu.be"))
                    {
                        var uri = new Uri(url);
                        string videoId = uri.Segments.Last();
                        string modElement = $"<div class='embed-responsive embed-responsive-16by9' style='float: none;'><iframe frameborder='0' src='//www.youtube.com/embed/{videoId}?rel=0&amp;loop=0&amp;origin=https://www.zapread.com' allowfullscreen='allowfullscreen' width='auto' height='auto' class='note-video-clip' style='float: none;'></iframe></div>";
                        var newNode = HtmlNode.CreateNode(modElement);
                        link.ParentNode.ReplaceChild(newNode, link);
                    }
                }
            }
            string contentStr = postDocument.DocumentNode.OuterHtml.SanitizeXSS();
            return contentStr;
        }

        private async Task AlertUsersNewPost(ZapContext db, User user, Post post, MailerController mailer)
        {
            var followUsers = db.Users
                .Include("Alerts")
                .Include("Settings")
                .Where(u => u.Following.Select(usr => usr.Id).Contains(user.Id));

            //testing - only email self
            //var followUsers = db.Users
            //    .Include("Alerts")
            //    .Include("Settings")
            //    .Where(u => u.Id == user.Id).ToList();

            mailer.ControllerContext = new ControllerContext(this.Request.RequestContext, mailer);
            string subject = "New post by user you are following: " + user.Name;
            string emailBody = await mailer.GenerateNewPostEmailBod(post.PostId, subject).ConfigureAwait(true);

            foreach (var u in followUsers)
            {
                // Add Alert
                var alert = new UserAlert()
                {
                    TimeStamp = DateTime.Now,
                    Title = "New post by a user you are following: <a href='" + @Url.Action(actionName: "Index", controllerName: "User", routeValues: new { username = user.Name }) + "'>" + user.Name + "</a>",
                    Content = "",//post.PostTitle,
                    IsDeleted = false,
                    IsRead = false,
                    To = u,
                    PostLink = post,
                };

                u.Alerts.Add(alert);

                if (u.Settings == null)
                {
                    u.Settings = new UserSettings();
                }

                if (u.Settings.NotifyOnNewPostSubscribedUser)
                {
                    string followerEmail = UserManager.FindById(u.AppId).Email;

                    // Enqueue emails for sending out.  Don't need to wait for this to finish before returning client response
                    BackgroundJob.Enqueue<MailingService>(x => x.SendI(
                        new UserEmailModel()
                        {
                            Destination = followerEmail,
                            Body = emailBody,
                            Email = "",
                            Name = "zapread.com",
                            Subject = subject,
                        }, "Notify", true));
                }
            }
            await db.SaveChangesAsync().ConfigureAwait(true);
        }

        private async Task AlertGroupNewPost(ZapContext db, Group postGroup, Post post)
        {
            var subusers = db.Users
                .Include("Alerts")
                .Where(u => u.Groups.Select(g => g.GroupId).Contains(postGroup.GroupId));

            foreach (var u in subusers)
            {
                // Add Alert
                var alert = new UserAlert()
                {
                    TimeStamp = DateTime.Now,
                    Title = "New post in subscribed group <a href='" + Url.Action(actionName: "GroupDetail", controllerName: "Group", routeValues: new { id = postGroup.GroupId }) + "'>" + postGroup.GroupName + "</a>",
                    Content = "",// "<a href='" + Url.Action(actionName:"Detail", controllerName: "Post", routeValues: new { post.PostId }) + "'>" + (post.PostTitle != null ? post.PostTitle : "Post") + "</a>",
                    IsDeleted = false,
                    IsRead = false,
                    To = u,
                    PostLink = post,
                };
                u.Alerts.Add(alert);
            }
            await db.SaveChangesAsync().ConfigureAwait(true);
        }

        public class DeletePostMsg
        {
            public int PostId { get; set; }
        }

        [HttpPost]
        public async Task<ActionResult> DeletePost(DeletePostMsg p)
        {
            var userId = User.Identity.GetUserId();
            using (var db = new ZapContext())
            {
                var post = db.Posts.Find(p.PostId);
                if (!User.IsInRole("Administrator"))
                {
                    if (post.UserId.AppId != userId)
                    {
                        return RedirectToAction("Index", "Home");
                    }
                }

                post.IsDeleted = true;
                await db.SaveChangesAsync().ConfigureAwait(false);

                return Json(new { Success = true });
                //return RedirectToAction("Index", "Home");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="PostId"></param>
        /// <param name="vote">0 = downvote, 1 = upvote</param>
        /// <param name="postTitle">Optional string which is used in SEO</param>
        /// <returns></returns>
        [MvcSiteMapNodeAttribute(Title = "Details", ParentKey = "Post", DynamicNodeProvider = "zapread.com.DI.PostsDetailsProvider, zapread.com")]
        [Route("Post/Detail/{PostId?}/{postTitle?}")]
        [HttpGet]
        [OutputCache(Duration = 600, VaryByParam = "*", Location = System.Web.UI.OutputCacheLocation.Downstream)]
        public async Task<ActionResult> Detail(int? PostId, string postTitle, int? vote)
        {
            if (PostId == null)
            {
                return RedirectToAction("Index", "Home");
            }

            using (var db = new ZapContext())
            {
                var userAppId = User.Identity.GetUserId();
                var userId = userAppId == null ? 0 : (await db.Users.FirstOrDefaultAsync(u => u.AppId == userAppId).ConfigureAwait(true))?.Id;

                if (userId.HasValue && userId != 0)
                {
                    await ClaimsHelpers.ValidateClaims(userId.Value, User).ConfigureAwait(true);
                }

                var pst = db.Posts
                    .Where(p => p.PostId == PostId && !p.IsDraft && !p.IsDeleted)
                    .Select(p => new PostViewModel()
                    {
                        PostTitle = p.PostTitle,
                        Content = p.Content,
                        PostId = p.PostId,
                        GroupId = p.Group.GroupId,
                        GroupName = p.Group.GroupName,
                        IsSticky = p.IsSticky,
                        UserName = p.UserId.Name,
                        UserId = p.UserId.Id,
                        UserAppId = p.UserId.AppId,
                        UserProfileImageVersion = p.UserId.ProfileImage.Version,
                        Score = p.Score,
                        TimeStamp = p.TimeStamp,
                        TimeStampEdited = p.TimeStampEdited,
                        IsNSFW = p.IsNSFW,
                        ViewerIsMod = userId.HasValue ? p.Group.Moderators.Select(m => m.Id).Contains(userId.Value) : false,
                        ViewerUpvoted = userId.HasValue ? p.VotesUp.Select(v => v.Id).Contains(userId.Value) : false,
                        ViewerDownvoted = userId.HasValue ? p.VotesDown.Select(v => v.Id).Contains(userId.Value) : false,
                        ViewerIgnoredUser = userId.HasValue ? (p.UserId.Id == userId.Value ? false : p.UserId.IgnoredByUsers.Select(u => u.Id).Contains(userId.Value)) : false,
                        CommentVms = p.Comments.Select(c => new PostCommentsViewModel()
                        {
                            PostId = p.PostId,
                            CommentId = c.CommentId,
                            Text = c.Text,
                            Score = c.Score,
                            IsReply = c.IsReply,
                            IsDeleted = c.IsDeleted,
                            TimeStamp = c.TimeStamp,
                            TimeStampEdited = c.TimeStampEdited,
                            UserId = c.UserId.Id,
                            UserName = c.UserId.Name,
                            UserAppId = c.UserId.AppId,
                            ProfileImageVersion = c.UserId.ProfileImage.Version,
                            ViewerUpvoted = userId.HasValue ? c.VotesUp.Select(v => v.Id).Contains(userId.Value) : false,
                            ViewerDownvoted = userId.HasValue ? c.VotesDown.Select(v => v.Id).Contains(userId.Value) : false,
                            ViewerIgnoredUser = userId.HasValue ? (c.UserId.Id == userId ? false : c.UserId.IgnoredByUsers.Select(u => u.Id).Contains(userId.Value)) : false,
                            ParentCommentId = c.Parent == null ? 0 : c.Parent.CommentId,
                            ParentUserId = c.Parent == null ? 0 : c.Parent.UserId.Id,
                            ParentUserName = c.Parent == null ? "" : c.Parent.UserId.Name,
                        }),
                    })
                    .AsNoTracking()
                    .FirstOrDefault();

                if (pst == null)
                {
                    return RedirectToAction("PostNotFound");
                }

                if (vote.HasValue)
                {
                    ViewBag.showVote = true;
                    ViewBag.vote = vote.Value;
                }

                return View(pst);
            }
        }

        public ActionResult PostNotFound()
        {
            return View();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="postId"></param>
        /// <param name="newLanguage"></param>
        /// <returns></returns>
        [HttpPost]
        public JsonResult ChangeLanguage(int postId, string newLanguage)
        {
            if (!User.IsInRole("Administrator"))
            {
                return Json(new { result = "error", success = false, message = "Admin role missing." });
            }

            using (var db = new ZapContext())
            {
                var post = db.Posts.SingleOrDefault(ps => ps.PostId == postId);

                if (post == null)
                {
                    return Json(new { result = "error", success = false, message = "Post not found in database." });
                }

                post.Language = newLanguage;

                db.SaveChanges();

                return Json(new { result = "success", success = true });
            }
        }
    }
}
