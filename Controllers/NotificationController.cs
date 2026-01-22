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
    public class NotificationController : Controller
    {
        private const int DefaultPageSize = 2;

        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICursorPagingService _pager;

        public NotificationController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ICursorPagingService pager)
        {
            _db = context;
            _userManager = userManager;
            _pager = pager;
        }

        private async Task<NotificationPageViewModel> BuildNotificationPageAsync(
            string userId,
            long? cursorTicks,
            int? cursorId,
            int pageSize)
        {
            var baseQuery = _db.Notifications
                .Where(n => n.UserId == userId)
                .Include(n => n.Actor)
                .Include(n => n.Group);

            var page = await _pager.PageAsync(
                baseQuery,
                new CursorPageRequest { PageSize = pageSize, CursorTicks = cursorTicks, CursorId = cursorId },
                n => n.CreatedAt,
                n => n.Id,
                descending: true);

            var items = page.Items.Select(n => new NotificationItemViewModel
            {
                Notification = n
            }).ToList();

            return new NotificationPageViewModel
            {
                Items = items,
                NextCursorTicks = page.NextCursorTicks,
                NextCursorId = page.NextCursorId,
                HasMore = page.HasMore,
                PageSize = page.PageSize
            };
        }

        public async Task<IActionResult> Index(int pageSize = DefaultPageSize)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            var model = await BuildNotificationPageAsync(currentUserId, null, null, pageSize);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> NotificationsPage(long? cursorTicks, int? cursorId, int pageSize = DefaultPageSize)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            var model = await BuildNotificationPageAsync(currentUserId, cursorTicks, cursorId, pageSize);
            return PartialView("_NotificationsPartial", model);
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            var notification = await _db.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == currentUserId);

            if (notification == null) return NotFound();

            notification.IsRead = true;
            await _db.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            await _db.Notifications
                .Where(n => n.UserId == currentUserId && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

            return RedirectToAction("Index");
        }
    }
}
