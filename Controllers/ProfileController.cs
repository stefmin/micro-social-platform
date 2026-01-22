using MicroSocialPlatform.Data;
using MicroSocialPlatform.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroSocialPlatform.Controllers
{
    public class ProfileController(ApplicationDbContext context, UserManager<ApplicationUser> userManager) : Controller

    {
        private readonly ApplicationDbContext db = context;
        private readonly UserManager<ApplicationUser> _userManager = userManager;

        [HttpGet("Profile/{username}")]
        public async Task<IActionResult> Index(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return NotFound();
            }

            var profileUser = await _userManager.FindByNameAsync(username);
            if (profileUser == null)
            {
                return NotFound();
            }

            var currentUserName = _userManager.GetUserName(User);
            var currentUserId = _userManager.GetUserId(User);
            bool isMe = currentUserName == username;
            bool isAdmin = User.IsInRole("Admin");

            ViewBag.IsMe = isMe;

            var followersCount = await db.UserFollows
                .CountAsync(f => f.TargetId == profileUser.Id && f.Status == FollowStatus.Accepted);

            var followingCount = await db.UserFollows
                .CountAsync(f => f.ObserverId == profileUser.Id && f.Status == FollowStatus.Accepted);

            ViewBag.FollowersCount = followersCount;
            ViewBag.FollowingCount = followingCount;


            bool isFollower = false;
            if (!isMe && currentUserId != null)
            {
                var followStatus = await db.UserFollows
                    .FirstOrDefaultAsync(f => f.ObserverId == currentUserId && f.TargetId == profileUser.Id);

                if (followStatus == null)
                {
                    ViewBag.FollowStatus = "None";
                }
                else
                {
                    ViewBag.FollowStatus = followStatus.Status.ToString();
                    isFollower = followStatus.Status == FollowStatus.Accepted;
                }
            }


            bool canViewContent = !profileUser.IsPrivate || isMe || isAdmin || isFollower;
            ViewBag.CanViewContent = canViewContent;

            if (canViewContent)
            {
                var posts = await db.Post
                    .Where(p => p.User.Id == profileUser.Id && p.GroupId == null)
                    .Include(p => p.User)
                    .Include(p => p.Likes)
                    .Include(p => p.Comments)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                ViewBag.Posts = posts;
                ViewBag.PostCount = posts.Count;
            }
            else
            {
                ViewBag.PostCount = await db.Post
                    .CountAsync(p => p.User.Id == profileUser.Id && p.GroupId == null);
            }

            return View(profileUser);
        }
    }
}