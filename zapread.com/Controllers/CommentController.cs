﻿using HtmlAgilityPack;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using zapread.com.Database;
using zapread.com.Models;
using zapread.com.Models.Database;
using zapread.com.Services;

namespace zapread.com.Controllers
{
    public class CommentController : Controller
    {
        private ApplicationUserManager _userManager;

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? Request.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        public class NewComment
        {
            [Required]
            [DataType(DataType.MultilineText)]
            [AllowHtml]
            public string CommentContent { get; set; }

            public int PostId { get; set; }
            public int CommentId { get; set; }
            public bool IsReply { get; set; }
            public bool IsDeleted { get; set; }
            public bool IsTest { get; set; }
        }

        [HttpPost, AllowAnonymous]
        public JsonResult GetMentions(string searchstr)
        {
            using (var db = new ZapContext())
            {
                var users = db.Users
                    .Where(u => u.Name.StartsWith(searchstr))
                    .Select(u => u.Name)
                    .Take(10)
                    .AsNoTracking()
                    .ToList();

                return Json(new { users });
            }
        }

        [HttpGet]
        public async Task<PartialViewResult> GetInputBox(int id)
        {
            Response.AddHeader("X-Frame-Options", "DENY");
            using (var db = new ZapContext())
            {
                Comment comment = await db.Comments
                    .Include(cmt => cmt.Post)
                    .FirstOrDefaultAsync(cmt => cmt.CommentId == id);
                return PartialView("_PartialCommentReplyInput", comment);
            }
        }

        [HttpGet]
        public ActionResult GetUserMentions()
        {
            using (var db = new ZapContext())
            {
                var usernames = db.Users.Select(u => u.Name).ToList();
                return Json(usernames, JsonRequestBehavior.AllowGet);
            }
        }

        public ActionResult DeleteComment(int Id)
        {
            var userId = User.Identity.GetUserId();

            using (var db = new ZapContext())
            {
                Comment comment = db.Comments.FirstOrDefault(cmt => cmt.CommentId == Id);
                if (comment == null)
                {
                    return Json(new { Success = false });
                }
                if (comment.UserId.AppId != userId)
                {
                    return Json(new { Success = false });
                }
                comment.IsDeleted = true;
                db.SaveChanges();
                return Json(new { Success = true });
            }
        }

        [HttpPost]
        public ActionResult UpdateComment(NewComment c)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { Success = false });
            }

            var userId = User.Identity.GetUserId();

            using (var db = new ZapContext())
            {
                var comment = db.Comments.FirstOrDefault(cmt => cmt.CommentId == c.CommentId);
                if (comment == null)
                {
                    return Json(new { Success = false, message = "Comment not found." });
                }
                if (comment.UserId.AppId != userId)
                {
                    return Json(new { Success = false, message = "User does not have rights to edit comment." });
                }
                comment.Text = SanitizeCommentXSS(c.CommentContent);
                comment.TimeStampEdited = DateTime.UtcNow;
                db.SaveChanges();
            }

            return Json(new
            {
                HTMLString = "",
                c.PostId,
                Success = true,
            });
        }


        /// <summary>
        /// Add a comment
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<ActionResult> AddComment(NewComment c)
        {
            // Check for empty comment
            if (c.CommentContent.Replace(" ", "") == "<p><br></p>")
            {
                return Json(new
                {
                    success = false,
                    message = "Error: Empty comment.",
                    c.PostId,
                    c.IsReply,
                    c.CommentId,
                });
            }

            var userId = User.Identity.GetUserId();

            using (var db = new ZapContext())
            {
                var user = await db.Users
                    .Include(usr => usr.Settings)
                    .Where(u => u.AppId == userId)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return this.Json(new
                    {
                        HTMLString = "",
                        c.PostId,
                        Success = false,
                        c.CommentId
                    });
                }

                var post = await db.Posts
                    .Include(pst => pst.UserId)
                    .Include(pst => pst.UserId.Settings)
                    .FirstOrDefaultAsync(p => p.PostId == c.PostId);

                if (post == null)
                {
                    return this.Json(new
                    {
                        HTMLString = "",
                        c.PostId,
                        Success = false,
                        c.CommentId
                    });
                }

                Comment parent = null;
                if (c.IsReply)
                {
                    parent = db.Comments.Include(cmt => cmt.Post).FirstOrDefault(cmt => cmt.CommentId == c.CommentId);
                }

                Comment comment = CreateComment(c, user, post, parent);

                var postOwner = post.UserId;
                if (postOwner.Settings == null)
                {
                    postOwner.Settings = new UserSettings();
                }

                // This is the owner of the comment being replied to
                User commentOwner = null;

                if (c.IsReply)
                {
                    commentOwner = db.Comments.FirstOrDefault(cmt => cmt.CommentId == c.CommentId).UserId;
                    if (commentOwner.Settings == null)
                    {
                        commentOwner.Settings = new UserSettings();
                    }
                }

                if (!c.IsReply)
                {
                    post.Comments.Add(comment);
                }

                if (!c.IsTest)
                {
                    db.Comments.Add(comment);
                    await db.SaveChangesAsync();
                }

                // Find user mentions
                try
                {
                    var doc = new HtmlDocument();
                    doc.LoadHtml(comment.Text);
                    var spans = doc.DocumentNode.SelectNodes("//span");
                    if (spans != null)
                    {
                        foreach (var s in spans)
                        {
                            if (!c.IsTest)
                                await NotifyUserMentioned(db, user, post, comment, s);
                        }
                    }
                }
                catch (Exception e)
                {
                    MailingService.Send(new UserEmailModel()
                    {
                        Destination = System.Configuration.ConfigurationManager.AppSettings["ExceptionReportEmail"],
                        Body = " Exception: " + e.Message + "\r\n Stack: " + e.StackTrace + "\r\n comment: " + c.CommentContent + "\r\n user: " + userId,
                        Email = "",
                        Name = "zapread.com Exception",
                        Subject = "User comment error",
                    });
                }

                // Only send messages if not own post.
                if (!c.IsReply && (postOwner.AppId != user.AppId))
                {
                    if (!c.IsTest)
                        await NotifyPostOwnerOfComment(db, user, post, comment, postOwner).ConfigureAwait(true);
                }

                if (c.IsReply && commentOwner.AppId != user.AppId)
                {
                    if (!c.IsTest)
                        await NotifyCommentOwnerOfReply(db, user, post, comment, commentOwner).ConfigureAwait(true);
                }

                string CommentHTMLString = RenderPartialViewToString("_PartialCommentRender", 
                    new PostCommentsViewModel() 
                    { 
                        StartVisible = true, 
                        Comment = comment, 
                        ParentComment = parent, 
                        Comments = new List<Comment>() 
                    });

                return this.Json(new
                {
                    HTMLString = CommentHTMLString,
                    c.PostId,
                    success = true,
                    c.IsReply,
                    c.CommentId,
                });
            }
        }

        [HttpPost]
        public async Task<ActionResult> LoadMoreComments(int postId, int? commentId, int? nestLevel, string rootshown)
        {
            using (var db = new ZapContext())
            {
                var post = await db.Posts
                    .Include(p => p.Comments)
                    .FirstOrDefaultAsync(p => p.PostId == postId);

                if (post == null)
                {
                    return HttpNotFound("Post not found");
                }

                var comment = await db.Comments
                    .Include(c => c.Post)
                    .Include(c => c.Post.Comments)
                    .FirstOrDefaultAsync(c => c.CommentId == commentId);

                if (comment == null && commentId != null)
                {
                    return HttpNotFound("Comment not found");
                }

                var userId = User.Identity.GetUserId();

                var shown = rootshown.Split(';').Select(s => Convert.ToInt64(s)).ToList();

                // these are the comments we will show
                var commentIds = post.Comments.Where(c => !shown.Contains(c.CommentId))
                    .Where(c => !c.IsReply)
                    .OrderByDescending(c => c.Score)
                    .ThenByDescending(c => c.TimeStamp)
                    .Select(c => c.CommentId)
                    .ToList();

                //.Include(p => p.Group)
                //        .Include(p => p.Comments)
                //        .Include(p => p.Comments.Select(cmt => cmt.Parent))
                //        .Include(p => p.Comments.Select(cmt => cmt.VotesUp))
                //        .Include(p => p.Comments.Select(cmt => cmt.VotesDown))
                //        .Include(p => p.Comments.Select(cmt => cmt.UserId))

                // All the comments related to this post
                var postComments = await db.Posts
                    .Include(p => p.Group)
                    .Include(p => p.Comments)
                    .Include(p => p.Comments.Select(c => c.Parent))
                    .Include(p => p.Comments.Select(c => c.VotesUp))
                    .Include(p => p.Comments.Select(c => c.VotesDown))
                    .Include(p => p.Comments.Select(c => c.UserId))
                    .Where(p => p.PostId == postId)
                    .SelectMany(p => p.Comments)
                    .ToListAsync();

                string CommentHTMLString = "";

                foreach (var cid in commentIds.Take(3)) // Comments in 3's
                {
                    var cmt = await db.Comments
                    .Include(c => c.Post)
                    .Include(c => c.Post.Comments)
                    .Include(c => c.Post.Comments.Select(cm => cm.Parent))
                    .Include(c => c.UserId)
                    .Include(c => c.VotesUp)
                    .Include(c => c.VotesDown)
                    .Include(c => c.Parent)
                    .Include(c => c.Parent.UserId)
                    .FirstOrDefaultAsync(c => c.CommentId == cid);

                    if (cmt == null)
                    {
                        return Json(new
                        {
                            success = true,
                            more = false,
                            HTMLString = ""
                        });
                    }

                    var vm = new PostCommentsViewModel
                    {
                        NestLevel = nestLevel ?? 1,
                        Comment = cmt,
                        Comments = postComments,
                        ViewerIgnoredUsers = new List<int>(),// Model.ViewerIgnoredUsers
                        StartVisible = cmt.Score >= 0,
                    };

                    CommentHTMLString += RenderPartialViewToString("_PartialCommentRender", vm);
                    shown.Add(cmt.CommentId);
                }

                return Json(new
                {
                    success = true,
                    shown = String.Join(";", shown),
                    hasMore = commentIds.Count() > 3,
                    HTMLString = CommentHTMLString
                });
            }
        }

        private static Comment CreateComment(NewComment c, User user, Post post, Comment parent)
        {
            // Sanitize for XSS
            string commentText = c.CommentContent;
            string sanitizedComment = SanitizeCommentXSS(commentText);

            return new Comment()
            {
                Parent = parent,
                IsReply = c.IsReply,
                UserId = user,
                Text = sanitizedComment,
                TimeStamp = DateTime.UtcNow,
                Post = post,
                Score = 0,
                TotalEarned = 0.0,
                VotesUp = new List<User>(),
                VotesDown = new List<User>(),
                IsDeleted = c.IsDeleted,
            };
        }

        private static string SanitizeCommentXSS(string commentText)
        {
            // Fix for nasty inject with odd brackets
            byte[] bytes = Encoding.Unicode.GetBytes(commentText);
            commentText = Encoding.Unicode.GetString(bytes);

            var sanitizer = new Ganss.XSS.HtmlSanitizer(
                allowedCssProperties: new[] { "color", "display", "text-align", "font-size", "margin-right", "width" },
                allowedCssClasses: new[] { "badge", "badge-info", "userhint", "blockquote", "img-fluid" });

            sanitizer.AllowedTags.Remove("button");

            sanitizer.AllowedAttributes.Add("class");
            sanitizer.AllowedAttributes.Remove("id");

            var sanitizedComment = sanitizer.Sanitize(commentText);
            return sanitizedComment;
        }

        private async Task NotifyUserMentioned(ZapContext db, User user, Post post, Comment comment, HtmlNode s)
        {
            if (s.Attributes.Count(a => a.Name == "class") > 0)
            {
                var cls = s.Attributes.FirstOrDefault(a => a.Name == "class");
                if (cls.Value.Contains("userhint"))
                {
                    var username = s.InnerHtml.Replace("@", "");
                    var mentioneduser = db.Users
                        .Include(usr => usr.Settings)
                        .FirstOrDefault(u => u.Name == username);

                    if (mentioneduser != null)
                    {
                        // Add Message
                        UserMessage message = CreateMentionedMessage(user, post, comment, mentioneduser);
                        mentioneduser.Messages.Add(message);
                        await db.SaveChangesAsync();

                        // Send Email
                        SendMentionedEmail(user, post, comment, mentioneduser);
                    }
                }
            }
        }

        private async Task NotifyPostOwnerOfComment(ZapContext db, User user, Post post, Comment comment, User postOwner)
        {
            // Add Alert
            if (postOwner.Settings == null)
            {
                postOwner.Settings = new UserSettings();
            }

            if (postOwner.Settings.AlertOnOwnPostCommented)
            {
                UserAlert alert = CreateCommentedAlert(user, post, comment, postOwner);
                postOwner.Alerts.Add(alert);
                await db.SaveChangesAsync();
            }

            // Send Email
            if (postOwner.Settings.NotifyOnOwnPostCommented)
            {
                string subject = "New comment on your post: " + post.PostTitle;
                string ownerEmail = UserManager.FindById(postOwner.AppId).Email;

                var mailer = DependencyResolver.Current.GetService<MailerController>();
                mailer.ControllerContext = new ControllerContext(this.Request.RequestContext, mailer);

                await mailer.SendPostComment(comment.CommentId, ownerEmail, subject);
            }
        }

        private async Task NotifyCommentOwnerOfReply(ZapContext db, User user, Post post, Comment comment, User commentOwner)
        {
            UserMessage message = CreateCommentRepliedMessage(user, post, comment, commentOwner);

            commentOwner.Messages.Add(message);
            await db.SaveChangesAsync();

            if (commentOwner.Settings == null)
            {
                commentOwner.Settings = new UserSettings();
            }

            if (commentOwner.Settings.NotifyOnOwnCommentReplied)
            {
                var cdoc = new HtmlDocument();
                cdoc.LoadHtml(comment.Text);
                var baseUri = new Uri("https://www.zapread.com/");
                var imgs = cdoc.DocumentNode.SelectNodes("//img/@src");
                if (imgs != null)
                {
                    foreach (var item in imgs)
                    {
                        item.SetAttributeValue("src", new Uri(baseUri, item.GetAttributeValue("src", "")).AbsoluteUri);
                    }
                }
                string commentContent = cdoc.DocumentNode.OuterHtml;

                string ownerEmail = UserManager.FindById(commentOwner.AppId).Email;
                MailingService.Send(user: "Notify",
                    message: new UserEmailModel()
                    {
                        Subject = "New reply to your comment in post: " + post.PostTitle,
                        Body = "From: <a href='http://www.zapread.com/user/" + user.Name.ToString() + "'>" + user.Name + "</a>"
                            + "<br/> " + commentContent
                            + "<br/><br/>Go to <a href='http://www.zapread.com/Post/Detail/" + post.PostId.ToString() + "'>" + (post.PostTitle != null ? post.PostTitle : "Post") + "</a> at <a href='http://www.zapread.com'>zapread.com</a>",
                        Destination = ownerEmail,
                        Email = "",
                        Name = "ZapRead.com Notify"
                    });
            }
        }

        private UserMessage CreateCommentRepliedMessage(User user, Post post, Comment comment, User commentOwner)
        {
            return new UserMessage()
            {
                TimeStamp = DateTime.Now,
                Title = "New reply to your comment in post: <a href='" + Url.Action(actionName: "Detail", controllerName: "Post", routeValues: new { id = post.PostId }) + "'>" + (post.PostTitle != null ? post.PostTitle : "Post") + "</a>",
                Content = comment.Text,
                CommentLink = comment,
                IsDeleted = false,
                IsRead = false,
                To = commentOwner,
                PostLink = post,
                From = user,
            };
        }

        private UserAlert CreateCommentedAlert(User user, Post post, Comment comment, User postOwner)
        {
            var alert = new UserAlert()
            {
                TimeStamp = DateTime.Now,
                Title = "New comment on your post: <a href=" + @Url.Action("Detail", "Post", new { post.PostId }) + ">" + post.PostTitle + "</a>",
                Content = "From: <a href='" + @Url.Action(actionName: "Index", controllerName: "User", routeValues: new { username = user.Name }) + "'>" + user.Name + "</a>",//< br/> " + c.CommentContent,
                CommentLink = comment,
                IsDeleted = false,
                IsRead = false,
                To = postOwner,
                PostLink = post,
            };
            return alert;
        }

        private void SendMentionedEmail(User user, Post post, Comment comment, User mentioneduser)
        {
            if (mentioneduser.Settings == null)
            {
                mentioneduser.Settings = new UserSettings();
            }

            if (mentioneduser.Settings.NotifyOnMentioned)
            {
                var cdoc = new HtmlDocument();
                cdoc.LoadHtml(comment.Text);
                var baseUri = new Uri("https://www.zapread.com/");
                var imgs = cdoc.DocumentNode.SelectNodes("//img/@src");
                if (imgs != null)
                {
                    foreach (var item in imgs)
                    {
                        item.SetAttributeValue("src", new Uri(baseUri, item.GetAttributeValue("src", "")).AbsoluteUri);
                    }
                }
                string commentContent = cdoc.DocumentNode.OuterHtml;

                string mentionedEmail = UserManager.FindById(mentioneduser.AppId).Email;
                MailingService.Send(user: "Notify",
                    message: new UserEmailModel()
                    {
                        Subject = "New mention in comment",
                        Body = "From: " + user.Name + "<br/> " + commentContent + "<br/><br/>Go to <a href='http://www.zapread.com/Post/Detail/" + post.PostId.ToString() + "'>post</a> at <a href='http://www.zapread.com'>zapread.com</a>",
                        Destination = mentionedEmail,
                        Email = "",
                        Name = "ZapRead.com Notify"
                    });
            }
        }

        private UserMessage CreateMentionedMessage(User user, Post post, Comment comment, User mentioneduser)
        {
            return new UserMessage()
            {
                TimeStamp = DateTime.Now,
                Title = "You were mentioned in a comment by <a href='" + @Url.Action(actionName: "Index", controllerName: "User", routeValues: new { username = user.Name }) + "'>" + user.Name + "</a>",
                Content = comment.Text,
                CommentLink = comment,
                IsDeleted = false,
                IsRead = false,
                To = mentioneduser,
                PostLink = post,
                From = user,
            };
        }

        protected string RenderPartialViewToString(string viewName, object model)
        {
            if (string.IsNullOrEmpty(viewName))
                viewName = ControllerContext.RouteData.GetRequiredString("action");

            ViewData.Model = model;

            using (StringWriter sw = new StringWriter())
            {
                ViewEngineResult viewResult =
                ViewEngines.Engines.FindPartialView(ControllerContext, viewName);
                ViewContext viewContext = new ViewContext
                (ControllerContext, viewResult.View, ViewData, TempData, sw);
                viewResult.View.Render(viewContext, sw);

                return sw.GetStringBuilder().ToString();
            }
        }

        private async Task EnsureUserExists(string userId, ZapContext db)
        {
            if (db.Users.Where(u => u.AppId == userId).Count() == 0)
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
}