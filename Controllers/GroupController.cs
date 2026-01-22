using MicroSocialPlatform.Data;
using MicroSocialPlatform.Models;
using MicroSocialPlatform.Services;
using MicroSocialPlatform.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroSocialPlatform.Services;

namespace MicroSocialPlatform.Controllers
{
    [Authorize]
    public class GroupController : Controller
    {
        private readonly ApplicationDbContext db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICursorPagingService _pagingService;
        private readonly IContentModerator _contentModerator;

        private const int DefaultPageSize = 2;

        public GroupController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ICursorPagingService pagingService, IContentModerator contentModerator)
        {
            db = context;
            _userManager = userManager;
            _pagingService = pagingService;
            _contentModerator = contentModerator;
        }

        private async Task<bool> IsUserSuspendedAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            return user?.Status == UserStatus.Suspended;
        }

        private async Task<bool> RejectIfHarmfulAsync(string? name, string? description)
        {
            var combined = string.Join("\n", new[] { name, description }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim()));

            if (string.IsNullOrWhiteSpace(combined))
                return false;

            var isHarmful = await _contentModerator.IsHarmfulAsync(combined);
            if (isHarmful)
            {
                ModelState.AddModelError("", "Your group content violates our guidelines. Please edit and try again.");
                return true;
            }

            return false;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Index(string? search, int pageSize = DefaultPageSize)
        {
            var model = await BuildGroupsPageAsync(search, null, null, pageSize);
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GroupsPage(string? search, long? cursorTicks, int? cursorId, int pageSize = DefaultPageSize)
        {
            var model = await BuildGroupsPageAsync(search, cursorTicks, cursorId, pageSize);
            return PartialView("_GroupsPartial", model);
        }

        private async Task<GroupsPageViewModel> BuildGroupsPageAsync(string? search, long? cursorTicks, int? cursorId, int pageSize)
        {
            if (pageSize <= 0) pageSize = DefaultPageSize;
            if (pageSize > 50) pageSize = 50;

            var query = db.Groups
                .Include(g => g.Owner)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var sNorm = search.Trim().ToUpperInvariant();

                query = query.Where(g =>
                    g.Name.ToUpper().Contains(sNorm) ||
                    (g.Description != null && g.Description.ToUpper().Contains(sNorm)));
            }

            var request = new CursorPageRequest
            {
                CursorTicks = cursorTicks,
                CursorId = cursorId,
                PageSize = pageSize
            };

            var result = await _pagingService.PageAsync(
                query,
                request,
                g => g.CreatedAt,
                g => g.Id,
                descending: true);

            return new GroupsPageViewModel
            {
                Items = result.Items.ToList(),
                Search = search,
                NextCursorTicks = result.NextCursorTicks,
                NextCursorId = result.NextCursorId,
                HasMore = result.HasMore,
                PageSize = result.PageSize
            };
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            var group = await db.Groups
                .Include(g => g.Owner)
                .Include(g => g.Members)
                    .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (group == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            bool isOwner = currentUserId == group.OwnerId;
            bool isMember = group.Members.Any(m => m.UserId == currentUserId && m.Status == MemberStatus.Accepted);
            bool hasPendingRequest = group.Members.Any(m => m.UserId == currentUserId && m.Status == MemberStatus.Pending);

            ViewBag.IsOwner = isOwner;
            ViewBag.IsMember = isMember;
            ViewBag.HasPendingRequest = hasPendingRequest;
            ViewBag.CanViewContent = group.IsPublic || isOwner || isMember;

            var memberCount = group.Members.Count(m => m.Status == MemberStatus.Accepted);
            ViewBag.MemberCount = memberCount;

            if (ViewBag.CanViewContent)
            {
                var posts = await db.Post
                    .Where(p => p.GroupId == id)
                    .Include(p => p.User)
                    .Include(p => p.Likes)
                    .Include(p => p.Comments)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                ViewBag.Posts = posts;
            }

            return View(group);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            if (await IsUserSuspendedAsync(currentUserId))
            {
                TempData["Error"] = "Your account is suspended. You cannot create new groups.";
                return RedirectToAction(nameof(Index));
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description,IsPublic,ImageUrl")] Group group)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();


            if (await IsUserSuspendedAsync(currentUserId))
            {
                TempData["Error"] = "Your account is suspended. You cannot create new groups.";
                return RedirectToAction(nameof(Index));
            }

            // doesn't need form validation but is required for model integrity; don't want to create a group without an owner
            ModelState.Remove("OwnerId");
            ModelState.Remove("Owner");

            if (!ModelState.IsValid)
                return View(group);

            if (await RejectIfHarmfulAsync(group.Name, group.Description))
            {
                return View(group);
            }

            group.OwnerId = currentUserId;
            group.CreatedAt = DateTime.UtcNow;

            db.Groups.Add(group);
            await db.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = group.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == id);
            if (group == null) return NotFound();
            if (group.OwnerId != currentUserId) return Forbid();

            return View(group);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,IsPublic,ImageUrl")] Group model)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            ModelState.Remove("OwnerId");
            ModelState.Remove("Owner");

            if (!ModelState.IsValid)
                return View(model);

            if (await RejectIfHarmfulAsync(model.Name, model.Description))
            {
                return View(model);
            }

            var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == id);
            if (group == null) return NotFound();
            if (group.OwnerId != currentUserId) return Forbid();

            group.Name = model.Name;
            group.Description = model.Description;
            group.IsPublic = model.IsPublic;
            group.ImageUrl = model.ImageUrl;

            await db.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = group.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == id);
            if (group == null) return NotFound();

            var isOwner = group.OwnerId == currentUserId;
            var isAdmin = User.IsInRole("Admin");
            if (!isOwner && !isAdmin) return Forbid();

            db.Groups.Remove(group);
            await db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Join(int id)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            if (await IsUserSuspendedAsync(currentUserId))
            {
                TempData["Error"] = "Your account is suspended. You cannot join groups.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == id);
            if (group == null) return NotFound();

            if (group.OwnerId == currentUserId)
                return RedirectToAction(nameof(Details), new { id });

            var existingMembership = await db.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == id && gm.UserId == currentUserId);

            if (existingMembership != null)
                return RedirectToAction(nameof(Details), new { id });

            var status = group.IsPublic ? MemberStatus.Accepted : MemberStatus.Pending;

            var membership = new GroupMember
            {
                GroupId = id,
                UserId = currentUserId,
                Status = status,
                JoinDate = DateTime.UtcNow
            };

            db.GroupMembers.Add(membership);

            if (!group.IsPublic)
            {
                var notification = new Notification
                {
                    UserId = group.OwnerId,
                    ActorId = currentUserId,
                    GroupId = group.Id,
                    Type = NotificationType.GroupJoinRequest,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };

                db.Notifications.Add(notification);
            }

            await db.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> Leave(int id)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == id);
            if (group == null) return NotFound();

            if (group.OwnerId == currentUserId)
                return RedirectToAction(nameof(Details), new { id });

            var membership = await db.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == id && gm.UserId == currentUserId);

            if (membership != null)
            {
                db.GroupMembers.Remove(membership);
                await db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> AcceptMember(int groupId, string userId)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null) return NotFound();
            if (group.OwnerId != currentUserId) return Forbid();

            var membership = await db.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId && gm.Status == MemberStatus.Pending);

            if (membership == null) return NotFound();

            membership.Status = MemberStatus.Accepted;

            var notification = new Notification
            {
                UserId = userId,
                ActorId = currentUserId,
                GroupId = groupId,
                Type = NotificationType.GroupJoinAccepted,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            db.Notifications.Add(notification);
            await db.SaveChangesAsync();

            return RedirectToAction(nameof(Members), new { id = groupId });
        }

        [HttpPost]
        public async Task<IActionResult> RejectMember(int groupId, string userId)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null) return NotFound();
            if (group.OwnerId != currentUserId) return Forbid();

            var membership = await db.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId && gm.Status == MemberStatus.Pending);

            if (membership == null) return NotFound();

            db.GroupMembers.Remove(membership);
            await db.SaveChangesAsync();

            return RedirectToAction(nameof(Members), new { id = groupId });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveMember(int groupId, string userId)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null) return NotFound();
            if (group.OwnerId != currentUserId) return Forbid();

            if (userId == currentUserId) return BadRequest();

            var membership = await db.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId);

            if (membership == null) return NotFound();

            db.GroupMembers.Remove(membership);
            await db.SaveChangesAsync();

            return RedirectToAction(nameof(Members), new { id = groupId });
        }

        [HttpGet]
        public async Task<IActionResult> Members(int id)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            var group = await db.Groups
                .Include(g => g.Members)
                    .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (group == null) return NotFound();
            if (group.OwnerId != currentUserId) return Forbid();

            ViewBag.PendingMembers = group.Members.Where(m => m.Status == MemberStatus.Pending).ToList();
            ViewBag.AcceptedMembers = group.Members.Where(m => m.Status == MemberStatus.Accepted).ToList();

            return View(group);
        }

        [HttpPost]
        public async Task<IActionResult> AcceptMemberFromNotification(int groupId, string userId, int notificationId)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null) return NotFound();
            if (group.OwnerId != currentUserId) return Forbid();

            var membership = await db.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId && gm.Status == MemberStatus.Pending);

            if (membership == null) return NotFound();

            membership.Status = MemberStatus.Accepted;

            var notification = await db.Notifications.FindAsync(notificationId);
            if (notification != null && notification.UserId == currentUserId)
                db.Notifications.Remove(notification);

            db.Notifications.Add(new Notification
            {
                UserId = userId,
                ActorId = currentUserId,
                GroupId = groupId,
                Type = NotificationType.GroupJoinAccepted,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });

            await db.SaveChangesAsync();
            return RedirectToAction("Index", "Notification");
        }

        [HttpPost]
        public async Task<IActionResult> RejectMemberFromNotification(int groupId, string userId, int notificationId)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Challenge();

            var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null) return NotFound();
            if (group.OwnerId != currentUserId) return Forbid();

            var membership = await db.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId && gm.Status == MemberStatus.Pending);

            if (membership == null) return NotFound();

            db.GroupMembers.Remove(membership);

            var notification = await db.Notifications.FindAsync(notificationId);
            if (notification != null && notification.UserId == currentUserId)
                db.Notifications.Remove(notification);

            await db.SaveChangesAsync();
            return RedirectToAction("Index", "Notification");
        }
    }
}
