//using Amazon.S3;
//using Amazon.S3.Model;
//using MicroSocialPlatform.Data;
//using MicroSocialPlatform.Models;
//using MicroSocialPlatform.ViewModels;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Identity;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;

//namespace MicroSocialPlatform.Controllers
//{
//    [Authorize]
//    [Route("Post")]
//    public class PostController : Controller
//    {
//        private readonly ApplicationDbContext _db;
//        private readonly UserManager<ApplicationUser> _userManager;
//        private readonly IAmazonS3 _s3;
//        private readonly IConfiguration _config;

//        public PostController(
//            ApplicationDbContext db,
//            UserManager<ApplicationUser> userManager,
//            IAmazonS3 s3,
//            IConfiguration config)
//        {
//            _db = db;
//            _userManager = userManager;
//            _s3 = s3;
//            _config = config;
//        }

//        private async Task<bool> CanCommentAsync(string currentUserId, ApplicationUser postOwner)
//        {
//            if (postOwner.Id == currentUserId) return true;
//            if (!postOwner.IsPrivate) return true;

//            return await _db.UserFollows.AnyAsync(f =>
//                f.ObserverId == currentUserId &&
//                f.TargetId == postOwner.Id &&
//                f.Status == FollowStatus.Accepted);
//        }

//        //Instagram-like feed
//        [HttpGet("Index")]
//        public async Task<IActionResult> Index()
//        {
//            var currentUserId = _userManager.GetUserId(User);
//            if (currentUserId == null) return Challenge();

//            // show: my posts + people I follow (Accepted)
//            var followingIds = await _db.UserFollows
//                .Where(f => f.ObserverId == currentUserId && f.Status == FollowStatus.Accepted)
//                .Select(f => f.TargetId)
//                .ToListAsync();

//            followingIds.Add(currentUserId);

//            var posts = await _db.Post
//                .Where(p => followingIds.Contains(p.User.Id))
//                .Include(p => p.User)
//                .Include(p => p.Comments)
//                    .ThenInclude(c => c.User)
//                .OrderByDescending(p => p.CreatedAt)
//                .ToListAsync();

//            // Likes (hearts): don't Include the Like entities here (it can get heavy).
//            // Instead, compute counts + "liked by me" sets via targeted queries.
//            var postIds = posts.Select(p => p.Id).ToList();
//            var commentIds = posts.SelectMany(p => p.Comments).Select(c => c.Id).ToList();

//            var postLikeCounts = await _db.Set<Like>()
//                .Where(l => l.PostId != null && postIds.Contains(l.PostId.Value))
//                .GroupBy(l => l.PostId!.Value)
//                .Select(g => new { PostId = g.Key, Count = g.Count() })
//                .ToDictionaryAsync(x => x.PostId, x => x.Count);

//            var likedPostIds = await _db.Set<Like>()
//                .Where(l => l.UserId == currentUserId && l.PostId != null && postIds.Contains(l.PostId.Value))
//                .Select(l => l.PostId!.Value)
//                .ToHashSetAsync();

//            var likedCommentIds = await _db.Set<Like>()
//                .Where(l => l.UserId == currentUserId && l.CommentId != null && commentIds.Contains(l.CommentId.Value))
//                .Select(l => l.CommentId!.Value)
//                .ToHashSetAsync();

//            var commentLikeCounts = await _db.Set<Like>()
//                .Where(l => l.CommentId != null && commentIds.Contains(l.CommentId.Value))
//                .GroupBy(l => l.CommentId!.Value)
//                .Select(g => new { CommentId = g.Key, Count = g.Count() })
//                .ToDictionaryAsync(x => x.CommentId, x => x.Count);

//            // For the current feed this will effectively always be true, but we compute it
//            // so the view can be reused if you later show public / explore posts too.
//            var acceptedFollowingSet = followingIds.ToHashSet(StringComparer.Ordinal);

//            var model = posts.Select(p => new PostFeedItemViewModel
//            {
//                Post = p,
//                CanComment = p.User.Id == currentUserId || !p.User.IsPrivate || acceptedFollowingSet.Contains(p.User.Id),

//                PostLikeCount = postLikeCounts.TryGetValue(p.Id, out var plc) ? plc : 0,
//                IsPostLikedByMe = likedPostIds.Contains(p.Id),

//                // The view can use this set to render a filled heart for comments liked by the current user.
//                // For per-comment counts, use the helper method below or query in the view model if you extend it.
//                LikedCommentIds = likedCommentIds,
//                CommentLikeCounts = commentLikeCounts

//            }).ToList();

//            return View(model);
//        }

//        [ValidateAntiForgeryToken]
//        [HttpPost("AddComment")]
//        public async Task<IActionResult> AddComment(int postId, string text)
//        {
//            var currentUserId = _userManager.GetUserId(User);
//            if (currentUserId == null) return Challenge();

//            if (string.IsNullOrWhiteSpace(text))
//            {
//                // Keep UX simple: just return to the feed.
//                return RedirectToAction(nameof(Index));
//            }

//            var post = await _db.Post
//                .Include(p => p.User)
//                .FirstOrDefaultAsync(p => p.Id == postId);
//            if (post == null) return NotFound();

//            if (!await CanCommentAsync(currentUserId, post.User))
//                return Forbid();

//            var currentUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == currentUserId);
//            if (currentUser == null) return Challenge();

//            var comment = new Comment
//            {
//                Text = text.Trim(),
//                Post = post,
//                User = currentUser,
//                CreatedAt = DateTime.UtcNow
//            };

//            _db.Set<Comment>().Add(comment);
//            await _db.SaveChangesAsync();

//            return RedirectToAction(nameof(Index));
//        }

//        [ValidateAntiForgeryToken]
//        [HttpPost("DeleteComment")]
//        public async Task<IActionResult> DeleteComment(int commentId)
//        {
//            var currentUserId = _userManager.GetUserId(User);
//            if (currentUserId == null) return Challenge();

//            var comment = await _db.Set<Comment>()
//                .Include(c => c.User)
//                .Include(c => c.Post)
//                    .ThenInclude(p => p.User)
//                .FirstOrDefaultAsync(c => c.Id == commentId);

//            if (comment == null) return NotFound();

//            var isAuthor = comment.User.Id == currentUserId;
//            var isPostOwner = comment.Post.User.Id == currentUserId;
//            if (!isAuthor && !isPostOwner) return Forbid();

//            _db.Set<Comment>().Remove(comment);
//            await _db.SaveChangesAsync();

//            return RedirectToAction(nameof(Index));
//        }

//        // -------- Likes (Instagram heart) --------
//        [ValidateAntiForgeryToken]
//        [HttpPost("TogglePostLike")]
//        public async Task<IActionResult> TogglePostLike(int postId)
//        {
//            var currentUserId = _userManager.GetUserId(User);
//            if (currentUserId == null) return Challenge();

//            var post = await _db.Post
//                .Include(p => p.User)
//                .FirstOrDefaultAsync(p => p.Id == postId);

//            if (post == null) return NotFound();

//            // Visibility/interaction gate (matches comment rules)
//            if (!await CanCommentAsync(currentUserId, post.User))
//                return Forbid();

//            var existing = await _db.Set<Like>()
//                .FirstOrDefaultAsync(l => l.UserId == currentUserId && l.PostId == postId);

//            bool liked;
//            if (existing != null)
//            {
//                _db.Set<Like>().Remove(existing);
//                liked = false;
//            }
//            else
//            {
//                _db.Set<Like>().Add(new Like
//                {
//                    UserId = currentUserId,
//                    PostId = postId
//                });
//                liked = true;
//            }

//            await _db.SaveChangesAsync();

//            var likeCount = await _db.Set<Like>().CountAsync(l => l.PostId == postId);

//            // If called via JS (fetch/ajax) you can use the JSON response.
//            // If called via normal form POST you can ignore the JSON and just redirect.
//            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
//                return Json(new { liked, likeCount });

//            return RedirectToAction(nameof(Index));
//        }

//        [ValidateAntiForgeryToken]
//        [HttpPost("ToggleCommentLike")]
//        public async Task<IActionResult> ToggleCommentLike(int commentId)
//        {
//            var currentUserId = _userManager.GetUserId(User);
//            if (currentUserId == null) return Challenge();

//            var comment = await _db.Set<Comment>()
//                .Include(c => c.Post)
//                    .ThenInclude(p => p.User)
//                .FirstOrDefaultAsync(c => c.Id == commentId);

//            if (comment == null) return NotFound();

//            // Visibility/interaction gate: based on the post owner's privacy/follow status
//            if (!await CanCommentAsync(currentUserId, comment.Post.User))
//                return Forbid();

//            var existing = await _db.Set<Like>()
//                .FirstOrDefaultAsync(l => l.UserId == currentUserId && l.CommentId == commentId);

//            bool liked;
//            if (existing != null)
//            {
//                _db.Set<Like>().Remove(existing);
//                liked = false;
//            }
//            else
//            {
//                _db.Set<Like>().Add(new Like
//                {
//                    UserId = currentUserId,
//                    CommentId = commentId
//                });
//                liked = true;
//            }

//            await _db.SaveChangesAsync();

//            var likeCount = await _db.Set<Like>().CountAsync(l => l.CommentId == commentId);

//            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
//                return Json(new { liked, likeCount });

//            return RedirectToAction(nameof(Index));
//        }

//        [HttpGet("Create")]
//        public IActionResult Create() => View();

//        [ValidateAntiForgeryToken]
//        [HttpPost("Create")]
//        public async Task<IActionResult> Create(string? title, string? description, IFormFile media)
//        {
//            var currentUserId = _userManager.GetUserId(User);
//            if (currentUserId == null) return Challenge();

//            if (media == null || media.Length == 0)
//            {
//                ModelState.AddModelError("", "Please choose an image/video.");
//                return View();
//            }

//            var bucket = _config["S3:BucketName"];
//            if (string.IsNullOrWhiteSpace(bucket))
//                return BadRequest("Missing S3:BucketName in configuration.");


//            var mediaType = media.ContentType ?? "application/octet-stream";
//            var ext = Path.GetExtension(media.FileName);
//            var key = $"posts/{currentUserId}/{Guid.NewGuid()}{ext}";

//            using (var stream = media.OpenReadStream())
//            {
//                var put = new PutObjectRequest
//                {
//                    BucketName = bucket,
//                    Key = key,
//                    InputStream = stream,
//                    ContentType = mediaType
//                };
//                await _s3.PutObjectAsync(put);
//            }

//            var post = new Post
//            {
//                Title = title,
//                Description = description,
//                MediaKey = $"{_config["S3:ServiceURL"]}/{_config["S3:BucketName"]}/{key}",
//                MediaType = mediaType.StartsWith("video/") ? "video" : "image",
//                User = _db.Users.FirstOrDefault(u => u.Id == currentUserId),
//                CreatedAt = DateTime.UtcNow
//            };

//            _db.Post.Add(post);
//            await _db.SaveChangesAsync();

//            return RedirectToAction(nameof(Index));
//        }

//        [HttpGet("Edit")]
//        public async Task<IActionResult> Edit(int id)
//        {
//            var currentUserId = _userManager.GetUserId(User);
//            if (currentUserId == null) return Challenge();

//            var post = await _db.Post.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id);
//            if (post == null) return NotFound();
//            if (post.User.Id != currentUserId) return Forbid();

//            return View(post);
//        }

//        [ValidateAntiForgeryToken]
//        [HttpPost("Edit")]
//        public async Task<IActionResult> Edit(int id, string? title, string? description, IFormFile? newMedia)
//        {
//            var currentUserId = _userManager.GetUserId(User);
//            if (currentUserId == null) return Challenge();

//            var post = await _db.Post
//                                    .Include(p => p.User)
//                                    .FirstOrDefaultAsync(p => p.Id == id);
//            if (post == null) return NotFound();

//            if (post.User.Id != currentUserId) return Forbid();

//            post.Title = title;
//            post.Description = description;

//            if (newMedia != null && newMedia.Length > 0)
//            {
//                var bucket = _config["S3:BucketName"];
//                if (string.IsNullOrWhiteSpace(bucket))
//                    return BadRequest("Missing S3:BucketName in configuration.");

//                // delete old
//                await _s3.DeleteObjectAsync(bucket, post.MediaKey);

//                // upload new
//                var ext = Path.GetExtension(newMedia.FileName);
//                var key = $"posts/{currentUserId}/{Guid.NewGuid()}{ext}";
//                using (var stream = newMedia.OpenReadStream())
//                {
//                    await _s3.PutObjectAsync(new PutObjectRequest
//                    {
//                        BucketName = bucket,
//                        Key = key,
//                        InputStream = stream,
//                        ContentType = newMedia.ContentType
//                    });
//                }

//                post.MediaKey = key;
//                post.MediaType = (newMedia.ContentType?.StartsWith("video/") ?? false) ? "video" : "image";
//            }

//            await _db.SaveChangesAsync();
//            return RedirectToAction(nameof(Index));
//        }

//        [ValidateAntiForgeryToken]
//        [HttpPost("Delete")]
//        public async Task<IActionResult> Delete(int id)
//        {
//            var currentUserId = _userManager.GetUserId(User);
//            if (currentUserId == null) return Challenge();

//            var post = await _db.Post
//                                    .Include(p => p.User)
//                                    .FirstOrDefaultAsync(p => p.Id == id);
//            if (post == null) return NotFound();
//            if (post.User.Id != currentUserId) return Forbid();

//            var bucket = _config["S3:BucketName"];
//            var serviceUrl = _config["S3:ServiceURL"]?.TrimEnd('/');
//            var bkTrimmed = _config["S3:BucketName"]?.Trim('/');
//            var prefix = $"{serviceUrl}/{bucket}/";

//            if (!string.IsNullOrWhiteSpace(bucket))
//            {
//                await _s3.DeleteObjectAsync(bucket, post.MediaKey.Substring(prefix.Length));
//            }

//            _db.Post.Remove(post);
//            await _db.SaveChangesAsync();

//            return RedirectToAction(nameof(Index));
//        }
//    }
//}



using Amazon.S3;
using Amazon.S3.Model;
using MicroSocialPlatform.Data;
using MicroSocialPlatform.Models;
using MicroSocialPlatform.Services;
using MicroSocialPlatform.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroSocialPlatform.Controllers
{
    [Authorize]
    [Route("Post")]
    public class PostController : Controller
    {
        private const int DefaultFeedPageSize = 2;
        private const int DefaultCommentsPageSize = 2;   // initial comments shown per post in feed
        private const int CommentsLoadMorePageSize = 2; // per "load more" click

        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAmazonS3 _s3;
        private readonly IConfiguration _config;
        private readonly ICursorPagingService _pager;
        private readonly IContentModerator _contentModerator;


        public PostController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IAmazonS3 s3,
            IConfiguration config,
            ICursorPagingService pager,
            IContentModerator contentModerator)
        {
            _db = db;
            _userManager = userManager;
            _s3 = s3;
            _config = config;
            _pager = pager;
            _contentModerator = contentModerator;
        }

        private async Task<bool> RejectIfHarmfulAsync(string? title, string? description)
        {
            var combined = string.Join("\n", new[] { title, description }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim()));

            if (string.IsNullOrWhiteSpace(combined))
                return false;

            var isHarmful = await _contentModerator.IsHarmfulAsync(combined);
            if (isHarmful)
            {
                ModelState.AddModelError("", "Your content violates our guidelines. Please edit and try again.");
                return true;
            }

            return false;
        }
        private async Task<bool> RejectIfHarmfulCommentAsync(string text)
        {
            var trimmed = text?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) return false;

            var isHarmful = await _contentModerator.IsHarmfulAsync(trimmed);
            if (isHarmful)
            {
                TempData["CommentError"] = "Your comment violates our guidelines. Please edit and try again.";
                return true;
            }
            return false;
        }

        private async Task<bool> CanCommentAsync(string currentUserId, ApplicationUser postOwner)
        {
            if (postOwner.Id == currentUserId) return true;
            if (!postOwner.IsPrivate) return true;

            return await _db.UserFollows.AnyAsync(f =>
                f.ObserverId == currentUserId &&
                f.TargetId == postOwner.Id &&
                f.Status == FollowStatus.Accepted);
        }

        private async Task<bool> IsUserSuspendedAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            return user?.Status == UserStatus.Suspended;
        }

        private string? TryExtractS3Key(string mediaKey)
        {
            if (string.IsNullOrWhiteSpace(mediaKey)) return null;

            var serviceUrl = _config["S3:ServiceURL"]?.TrimEnd('/');
            var bucket = _config["S3:BucketName"]?.Trim('/');
            if (!string.IsNullOrWhiteSpace(serviceUrl) && !string.IsNullOrWhiteSpace(bucket))
            {
                var prefix = $"{serviceUrl}/{bucket}/";
                if (mediaKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return mediaKey.Substring(prefix.Length);
            }

            return mediaKey;
        }

        private async Task<FeedPageViewModel> BuildFeedPageAsync(
            string currentUserId,
            long? cursorTicks,
            int? cursorId,
            int pageSize)
        {
            // Who do we show in the feed?
            var followingIds = await _db.UserFollows
                .Where(f => f.ObserverId == currentUserId && f.Status == FollowStatus.Accepted)
                .Select(f => f.TargetId)
                .ToListAsync();

            followingIds.Add(currentUserId);
            var acceptedFollowingSet = followingIds.ToHashSet(StringComparer.Ordinal);

            // Base query (no ordering here; pager will apply ordering)
            var baseQuery = _db.Post
                .Where(p => followingIds.Contains(p.User.Id))
                .Include(p => p.User);

            // Page posts using the generic pager
            var postPage = await _pager.PageAsync(
                baseQuery,
                new CursorPageRequest { PageSize = pageSize, CursorTicks = cursorTicks, CursorId = cursorId },
                p => p.CreatedAt,
                p => p.Id,
                descending: true);

            var posts = postPage.Items.ToList();
            var postIds = posts.Select(p => p.Id).ToList();

            // Total comments count per post (fast, uses shadow FK PostId)
            var commentCounts = new Dictionary<int, int>();
            if (postIds.Count > 0)
            {
                commentCounts = await _db.Set<Comment>()
                    .Where(c => postIds.Contains(EF.Property<int>(c, "PostId")))
                    .GroupBy(c => EF.Property<int>(c, "PostId"))
                    .Select(g => new { PostId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.PostId, x => x.Count);
            }

            // Load FIRST PAGE of comments per post (uses the same pager) — keeps feed light
            var initialCommentsByPostId = new Dictionary<int, CursorPageResult<Comment>>();
            foreach (var post in posts)
            {
                var cq = _db.Set<Comment>()
                    .Where(c => EF.Property<int>(c, "PostId") == post.Id)
                    .Include(c => c.User);

                var commentsPage = await _pager.PageAsync(
                    cq,
                    new CursorPageRequest { PageSize = DefaultCommentsPageSize },
                    c => c.CreatedAt,
                    c => c.Id,
                    descending: false); // oldest -> newest

                initialCommentsByPostId[post.Id] = commentsPage;
            }

            // Likes (posts)
            var postLikeCounts = new Dictionary<int, int>();
            var likedPostIds = new HashSet<int>();
            if (postIds.Count > 0)
            {
                postLikeCounts = await _db.Set<Like>()
                    .Where(l => l.PostId != null && postIds.Contains(l.PostId.Value))
                    .GroupBy(l => l.PostId!.Value)
                    .Select(g => new { PostId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.PostId, x => x.Count);

                likedPostIds = await _db.Set<Like>()
                    .Where(l => l.UserId == currentUserId && l.PostId != null && postIds.Contains(l.PostId.Value))
                    .Select(l => l.PostId!.Value)
                    .ToHashSetAsync();
            }

            // Likes (comments in the loaded first-page comments)
            var loadedCommentIds = initialCommentsByPostId.Values
                .SelectMany(v => v.Items)
                .Select(c => c.Id)
                .ToList();

            var likedCommentIds = new HashSet<int>();
            var commentLikeCounts = new Dictionary<int, int>();

            if (loadedCommentIds.Count > 0)
            {
                likedCommentIds = await _db.Set<Like>()
                    .Where(l => l.UserId == currentUserId && l.CommentId != null && loadedCommentIds.Contains(l.CommentId.Value))
                    .Select(l => l.CommentId!.Value)
                    .ToHashSetAsync();

                commentLikeCounts = await _db.Set<Like>()
                    .Where(l => l.CommentId != null && loadedCommentIds.Contains(l.CommentId.Value))
                    .GroupBy(l => l.CommentId!.Value)
                    .Select(g => new { CommentId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.CommentId, x => x.Count);
            }

            var sharedLikedCommentIds = (ISet<int>)likedCommentIds;
            var sharedCommentLikeCounts = (IReadOnlyDictionary<int, int>)commentLikeCounts;

            var items = posts.Select(p =>
            {
                var commentsPage = initialCommentsByPostId.TryGetValue(p.Id, out var cp) ? cp : new CursorPageResult<Comment>();
                var totalCount = commentCounts.TryGetValue(p.Id, out var cc) ? cc : 0;

                return new PostFeedItemViewModel
                {
                    Post = p,
                    CanComment = p.User.Id == currentUserId || !p.User.IsPrivate || acceptedFollowingSet.Contains(p.User.Id),

                    PostLikeCount = postLikeCounts.TryGetValue(p.Id, out var plc) ? plc : 0,
                    IsPostLikedByMe = likedPostIds.Contains(p.Id),

                    TotalCommentCount = totalCount,

                    Comments = commentsPage.Items.ToList(),
                    LikedCommentIds = sharedLikedCommentIds,
                    CommentLikeCounts = sharedCommentLikeCounts,

                    CommentsHasMore = commentsPage.HasMore,
                    CommentsNextCursorTicks = commentsPage.NextCursorTicks,
                    CommentsNextCursorId = commentsPage.NextCursorId,
                    CommentsPageSize = DefaultCommentsPageSize
                };
            }).ToList();

            return new FeedPageViewModel
            {
                Items = items,
                NextCursorTicks = postPage.NextCursorTicks,
                NextCursorId = postPage.NextCursorId,
                HasMore = postPage.HasMore,
                PageSize = postPage.PageSize
            };
        }

        // ---------------- Feed ----------------

        [HttpGet("Index")]
        [AllowAnonymous]
        public async Task<IActionResult> Index(int pageSize = DefaultFeedPageSize)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null)
            {
                // Return empty feed for unauthenticated users
                return View(new FeedPageViewModel { Items = new(), HasMore = false });
            }

            var model = await BuildFeedPageAsync(currentUserId, null, null, pageSize);
            return View(model);
        }

        [HttpGet("FeedPage")]
        public async Task<IActionResult> FeedPage(long? cursorTicks, int? cursorId, int pageSize = DefaultFeedPageSize)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            var model = await BuildFeedPageAsync(currentUserId, cursorTicks, cursorId, pageSize);
            return PartialView("_FeedPostsPartial", model);
        }

        // ---------------- Comment paging (applies same pager to comments) ----------------

        [HttpGet("CommentsPage")]
        public async Task<IActionResult> CommentsPage(int postId, long? cursorTicks, int? cursorId, int pageSize = CommentsLoadMorePageSize)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            var post = await _db.Post
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == postId);

            if (post == null) return NotFound();

            // You can tighten this to a separate "CanViewPost" if you prefer, but this matches your rules well.
            if (!await CanCommentAsync(currentUserId, post.User))
                return Forbid();

            var cq = _db.Set<Comment>()
                .Where(c => EF.Property<int>(c, "PostId") == postId)
                .Include(c => c.User);

            var commentsPage = await _pager.PageAsync(
                cq,
                new CursorPageRequest { PageSize = pageSize, CursorTicks = cursorTicks, CursorId = cursorId },
                c => c.CreatedAt,
                c => c.Id,
                descending: false);

            var commentIds = commentsPage.Items.Select(c => c.Id).ToList();

            var likedCommentIds = new HashSet<int>();
            var commentLikeCounts = new Dictionary<int, int>();

            if (commentIds.Count > 0)
            {
                likedCommentIds = await _db.Set<Like>()
                    .Where(l => l.UserId == currentUserId && l.CommentId != null && commentIds.Contains(l.CommentId.Value))
                    .Select(l => l.CommentId!.Value)
                    .ToHashSetAsync();

                commentLikeCounts = await _db.Set<Like>()
                    .Where(l => l.CommentId != null && commentIds.Contains(l.CommentId.Value))
                    .GroupBy(l => l.CommentId!.Value)
                    .Select(g => new { CommentId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.CommentId, x => x.Count);
            }

            var vm = new CommentsPageViewModel
            {
                PostId = postId,
                Comments = commentsPage.Items.ToList(),
                LikedCommentIds = likedCommentIds,
                CommentLikeCounts = commentLikeCounts,
                HasMore = commentsPage.HasMore,
                NextCursorTicks = commentsPage.NextCursorTicks,
                NextCursorId = commentsPage.NextCursorId,
                PageSize = commentsPage.PageSize
            };

            return PartialView("_PostCommentsPartial", vm);
        }

        // ---------------- Comments CRUD ----------------

        [ValidateAntiForgeryToken]
        [HttpPost("AddComment")]
        public async Task<IActionResult> AddComment(int postId, string text)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            if (await IsUserSuspendedAsync(currentUserId))
            {
                TempData["Error"] = "Your account is suspended. You cannot create new content.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(text))
                return RedirectToAction(nameof(Index));

            var post = await _db.Post
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == postId);
            if (post == null) return NotFound();

            if (!await CanCommentAsync(currentUserId, post.User))
                return Forbid();

            var currentUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == currentUserId);
            if (currentUser == null) return Challenge();

            var trimmed = text?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return RedirectToAction(nameof(Index));

            if (await RejectIfHarmfulCommentAsync(trimmed))
                return RedirectToAction(nameof(Index));

            var comment = new Comment
            {
                Text = text.Trim(),
                Post = post,
                User = currentUser,
                CreatedAt = DateTime.UtcNow
            };

            _db.Set<Comment>().Add(comment);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [ValidateAntiForgeryToken]
        [HttpPost("DeleteComment")]
        public async Task<IActionResult> DeleteComment(int commentId)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            var comment = await _db.Set<Comment>()
                .Include(c => c.User)
                .Include(c => c.Post)
                    .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(c => c.Id == commentId);

            if (comment == null) return NotFound();

            var isAuthor = comment.User.Id == currentUserId;
            var isPostOwner = comment.Post.User.Id == currentUserId;
            var isAdmin = User.IsInRole("Admin");
            if (!isAuthor && !isPostOwner && !isAdmin) return Forbid();

            _db.Set<Comment>().Remove(comment);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ---------------- Likes (existing) ----------------

        [ValidateAntiForgeryToken]
        [HttpPost("TogglePostLike")]
        public async Task<IActionResult> TogglePostLike(int postId)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();


            if (await IsUserSuspendedAsync(currentUserId))
            {
                TempData["Error"] = "Your account is suspended. You cannot interact with content.";
                return RedirectToAction(nameof(Index));
            }

            var post = await _db.Post.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == postId);
            if (post == null) return NotFound();
            if (!await CanCommentAsync(currentUserId, post.User)) return Forbid();

            var existing = await _db.Set<Like>().FirstOrDefaultAsync(l => l.UserId == currentUserId && l.PostId == postId);

            bool liked;
            if (existing != null)
            {
                _db.Set<Like>().Remove(existing);
                liked = false;
            }
            else
            {
                _db.Set<Like>().Add(new Like { UserId = currentUserId, PostId = postId });
                liked = true;
            }

            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateException)
            {
                liked = await _db.Set<Like>().AnyAsync(l => l.UserId == currentUserId && l.PostId == postId);
            }

            var likeCount = await _db.Set<Like>().CountAsync(l => l.PostId == postId);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { liked, likeCount });

            return RedirectToAction(nameof(Index));
        }

        [ValidateAntiForgeryToken]
        [HttpPost("ToggleCommentLike")]
        public async Task<IActionResult> ToggleCommentLike(int commentId)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            if (await IsUserSuspendedAsync(currentUserId))
            {
                TempData["Error"] = "Your account is suspended. You cannot interact with content.";
                return RedirectToAction(nameof(Index));
            }

            var comment = await _db.Set<Comment>()
                .Include(c => c.Post).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(c => c.Id == commentId);

            if (comment == null) return NotFound();
            if (!await CanCommentAsync(currentUserId, comment.Post.User)) return Forbid();

            var existing = await _db.Set<Like>().FirstOrDefaultAsync(l => l.UserId == currentUserId && l.CommentId == commentId);

            bool liked;
            if (existing != null)
            {
                _db.Set<Like>().Remove(existing);
                liked = false;
            }
            else
            {
                _db.Set<Like>().Add(new Like { UserId = currentUserId, CommentId = commentId });
                liked = true;
            }

            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateException)
            {
                liked = await _db.Set<Like>().AnyAsync(l => l.UserId == currentUserId && l.CommentId == commentId);
            }

            var likeCount = await _db.Set<Like>().CountAsync(l => l.CommentId == commentId);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { liked, likeCount });

            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Create")]
        public async Task<IActionResult> Create(int? groupId)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            if (await IsUserSuspendedAsync(currentUserId))
            {
                TempData["Error"] = "Your account is suspended. You cannot create new content.";
                return RedirectToAction(nameof(Index));
            }

            if (groupId.HasValue)
            {
                var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == groupId.Value);
                if (group == null) return NotFound();

                bool isOwner = group.OwnerId == currentUserId;
                bool isMember = await _db.GroupMembers
                    .AnyAsync(gm => gm.GroupId == groupId.Value && gm.UserId == currentUserId && gm.Status == MemberStatus.Accepted);

                if (!isOwner && !isMember)
                {
                    return Forbid();
                }

                ViewBag.GroupId = groupId.Value;
                ViewBag.GroupName = group.Name;
            }

            return View();
        }

        [ValidateAntiForgeryToken]
        [HttpPost("Create")]
        public async Task<IActionResult> Create(string? title, string? description, IFormFile media, int? groupId)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();
            if (await IsUserSuspendedAsync(currentUserId))
            {
                TempData["Error"] = "Your account is suspended. You cannot create new content.";
                return RedirectToAction(nameof(Index));
            }
            if (await RejectIfHarmfulAsync(title, description))
            {
                return View();
            }
            // check if user can post to group (must be owner or member)
            if (groupId.HasValue)
            {
                var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == groupId.Value);
                if (group == null) return NotFound();

                bool isOwner = group.OwnerId == currentUserId;
                bool isMember = await _db.GroupMembers
                    .AnyAsync(gm => gm.GroupId == groupId.Value && gm.UserId == currentUserId && gm.Status == MemberStatus.Accepted);

                if (!isOwner && !isMember)
                {
                    return Forbid();
                }

                ViewBag.GroupId = groupId.Value;
                ViewBag.GroupName = group.Name;
            }

            if (media == null || media.Length == 0)
            {
                ModelState.AddModelError("", "Please choose an image/video.");
                return View();
            }

            var bucket = _config["S3:BucketName"];
            if (string.IsNullOrWhiteSpace(bucket))
                return BadRequest("Missing S3:BucketName in configuration.");


            var mediaType = media.ContentType ?? "application/octet-stream";
            var ext = Path.GetExtension(media.FileName);
            var key = $"posts/{currentUserId}/{Guid.NewGuid()}{ext}";

            using (var stream = media.OpenReadStream())
            {
                var put = new PutObjectRequest
                {
                    BucketName = bucket,
                    Key = key,
                    InputStream = stream,
                    ContentType = mediaType
                };
                await _s3.PutObjectAsync(put);
            }

            var post = new Post
            {
                Title = title,
                Description = description,
                MediaKey = $"{_config["S3:ServiceURL"]}/{_config["S3:BucketName"]}/{key}",
                MediaType = mediaType.StartsWith("video/") ? "video" : "image",
                User = _db.Users.FirstOrDefault(u => u.Id == currentUserId),
                CreatedAt = DateTime.UtcNow,
                GroupId = groupId
            };

            _db.Post.Add(post);
            await _db.SaveChangesAsync();

            if (groupId.HasValue)
            {
                return RedirectToAction("Details", "Group", new { id = groupId.Value });
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            var post = await _db.Post.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return NotFound();
            if (post.User.Id != currentUserId) return Forbid();

            return View(post);
        }

        [ValidateAntiForgeryToken]
        [HttpPost("Edit")]
        public async Task<IActionResult> Edit(int id, string? title, string? description, IFormFile? newMedia)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            var post = await _db.Post
                                    .Include(p => p.User)
                                    .FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return NotFound();

            if (post.User.Id != currentUserId) return Forbid();

            post.Title = title;
            post.Description = description;

            if (await RejectIfHarmfulAsync(title, description))
            {
                return View();
            }

            if (newMedia != null && newMedia.Length > 0)
            {
                var bucket = _config["S3:BucketName"];
                if (string.IsNullOrWhiteSpace(bucket))
                    return BadRequest("Missing S3:BucketName in configuration.");

                // delete old
                await _s3.DeleteObjectAsync(bucket, post.MediaKey);

                // upload new
                var ext = Path.GetExtension(newMedia.FileName);
                var key = $"posts/{currentUserId}/{Guid.NewGuid()}{ext}";
                using (var stream = newMedia.OpenReadStream())
                {
                    await _s3.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = bucket,
                        Key = key,
                        InputStream = stream,
                        ContentType = newMedia.ContentType
                    });
                }

                post.MediaKey = key;
                post.MediaType = (newMedia.ContentType?.StartsWith("video/") ?? false) ? "video" : "image";
            }

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [ValidateAntiForgeryToken]
        [HttpPost("Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            var post = await _db.Post
                                    .Include(p => p.User)
                                    .FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return NotFound();

            var isOwner = post.User.Id == currentUserId;
            var isAdmin = User.IsInRole("Admin");
            if (!isOwner && !isAdmin) return Forbid();

            var bucket = _config["S3:BucketName"];
            var serviceUrl = _config["S3:ServiceURL"]?.TrimEnd('/');
            var bkTrimmed = _config["S3:BucketName"]?.Trim('/');
            var prefix = $"{serviceUrl}/{bucket}/";

            if (!string.IsNullOrWhiteSpace(bucket))
            {
                await _s3.DeleteObjectAsync(bucket, post.MediaKey.Substring(prefix.Length));
            }

            _db.Post.Remove(post);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}