using MicroSocialPlatform.Data;
using MicroSocialPlatform.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroSocialPlatform.Controllers
{
    [Authorize]
    public class FollowController(ApplicationDbContext context, UserManager<ApplicationUser> userManager) : Controller
    {
        private readonly ApplicationDbContext db = context;
        private readonly UserManager<ApplicationUser> _userManager = userManager;

        private async Task<bool> IsUserSuspendedAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            return user?.Status == UserStatus.Suspended;
        }

        [HttpPost]
        public async Task<IActionResult> ToggleFollow(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest();

            var currentUserId = _userManager.GetUserId(User);

            if (currentUserId == null) return Challenge(); // return unauthorized

            if (await IsUserSuspendedAsync(currentUserId))
            {
                TempData["Error"] = "Your account is suspended. You cannot follow users.";
                return RedirectToAction("Index", "Profile", new { username = User.Identity!.Name });
            }

            // one cannot follow himself
            if (currentUserId == id) return RedirectToAction("Index", "Profile", new { username = User.Identity!.Name });

            var existingFollow = await db.UserFollows
                .FirstOrDefaultAsync(f => f.ObserverId == currentUserId && f.TargetId == id);

            ApplicationUser? targetUser = await _userManager.FindByIdAsync(id);
            if (targetUser == null) return NotFound();

            if (existingFollow != null)
            {
                db.UserFollows.Remove(existingFollow);
                await db.SaveChangesAsync();
            }
            else
            {
                var status = targetUser.IsPrivate ? FollowStatus.Pending : FollowStatus.Accepted;

                var newFollow = new UserFollow
                {
                    ObserverId = currentUserId,
                    TargetId = targetUser.Id,
                    Status = status,
                    RequestDate = DateTime.Now,
                };

                db.UserFollows.Add(newFollow);

                // send notification to target user
                var notificationType = targetUser.IsPrivate ? NotificationType.FollowRequest : NotificationType.NewFollower;

                var notification = new Notification
                {
                    UserId = targetUser.Id,
                    ActorId = currentUserId,
                    Type = notificationType,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };

                db.Notifications.Add(notification);
                await db.SaveChangesAsync();
            }
            return RedirectToAction("Index", "Profile", new { username = targetUser.UserName });
        }

        [HttpPost]
        public async Task<IActionResult> AcceptFollow(string id)
        {
            var currentUserId = _userManager.GetUserId(User);

            var incomingFollow = await db.UserFollows.FirstOrDefaultAsync(
                    f => f.ObserverId == id && f.TargetId == currentUserId && f.Status == FollowStatus.Pending
                );
            if (incomingFollow == null) return NotFound();

            incomingFollow.Status = FollowStatus.Accepted;

            // notify the requester that their follow request was accepted
            var notification = new Notification
            {
                UserId = id,
                ActorId = currentUserId,
                Type = NotificationType.FollowAccepted,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            db.Notifications.Add(notification);
            await db.SaveChangesAsync();

            return RedirectToAction("Index", "Profile", new { username = User.Identity!.Name });

        }

        [HttpPost]
        public async Task<IActionResult> RejectFollow(string id)
        {
            var currentUserId = _userManager.GetUserId(User);

            var incomingFollow = await db.UserFollows.FirstOrDefaultAsync(
                    f => f.ObserverId == id && f.TargetId == currentUserId && f.Status == FollowStatus.Pending
                );
            if (incomingFollow == null) return NotFound();

            db.UserFollows.Remove(incomingFollow);
            await db.SaveChangesAsync();

            return RedirectToAction("Index", "Profile", new { username = User.Identity!.Name });

        }

        [HttpPost]
        public async Task<IActionResult> AcceptFollowFromNotification(string id, int notificationId)
        {
            var currentUserId = _userManager.GetUserId(User);

            var incomingFollow = await db.UserFollows.FirstOrDefaultAsync(
                    f => f.ObserverId == id && f.TargetId == currentUserId && f.Status == FollowStatus.Pending
                );
            if (incomingFollow == null) return NotFound();

            incomingFollow.Status = FollowStatus.Accepted;

            // mark the notification as read & transform it
            var notification = await db.Notifications.FindAsync(notificationId);
            if (notification != null && notification.UserId == currentUserId)
            {
                notification.Type = NotificationType.NewFollower;
                notification.IsRead = true;
            }

            // notify the requester that their follow request was accepted
            var acceptedNotification = new Notification
            {
                UserId = id,
                ActorId = currentUserId,
                Type = NotificationType.FollowAccepted,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            db.Notifications.Add(acceptedNotification);
            await db.SaveChangesAsync();

            return RedirectToAction("Index", "Notification");
        }

        [HttpPost]
        public async Task<IActionResult> RejectFollowFromNotification(string id, int notificationId)
        {
            var currentUserId = _userManager.GetUserId(User);

            var incomingFollow = await db.UserFollows.FirstOrDefaultAsync(
                    f => f.ObserverId == id && f.TargetId == currentUserId && f.Status == FollowStatus.Pending
                );
            if (incomingFollow == null) return NotFound();

            db.UserFollows.Remove(incomingFollow);

            // remove the notification
            var notification = await db.Notifications.FindAsync(notificationId);
            if (notification != null && notification.UserId == currentUserId)
            {
                db.Notifications.Remove(notification);
            }

            await db.SaveChangesAsync();

            return RedirectToAction("Index", "Notification");
        }
    }
}
