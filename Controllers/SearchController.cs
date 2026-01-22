using MicroSocialPlatform.Data;
using MicroSocialPlatform.Models;
using MicroSocialPlatform.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroSocialPlatform.Controllers
{
    public class SearchController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        // Reasonable default (you can change)
        private const int DefaultPageSize = 2;

        public SearchController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _db = context;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Index()
        {
            // Empty search page - results load via AJAX
            return View(new SearchProfilePageViewModel { PageSize = DefaultPageSize });
        }

        [HttpGet]
        public async Task<IActionResult> SearchProfiles(string? query, string? cursor, int pageSize = DefaultPageSize)
        {
            var currentUserId = _userManager.GetUserId(User);

            // Normalize page size
            if (pageSize <= 0) pageSize = DefaultPageSize;
            if (pageSize > 50) pageSize = 50;

            var model = new SearchProfilePageViewModel
            {
                Query = query,
                PageSize = pageSize
            };

            // Return empty results if no query
            if (string.IsNullOrWhiteSpace(query))
            {
                model.Items = new();
                model.HasMore = false;
                model.NextCursor = null;
                return PartialView("_SearchResultsPartial", model);
            }

            var q = query.Trim();
            // Identity’s NormalizedUserName is typically UPPERCASE, so normalize query similarly
            var qNorm = q.ToUpperInvariant();

            // Parse cursor: "NORM|ID"
            string? cursorNorm = null;
            string? cursorId = null;
            if (!string.IsNullOrWhiteSpace(cursor))
            {
                var parts = cursor.Split('|', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 1) cursorNorm = parts[0];
                if (parts.Length == 2) cursorId = parts[1];
            }

            // Build query (case-insensitive) using NormalizedUserName
            var usersQuery = _db.Users.AsQueryable();

            usersQuery = usersQuery.Where(u =>
                u.NormalizedUserName != null &&
                u.NormalizedUserName.Contains(qNorm));

            // Apply stable cursor (NormalizedUserName, Id)
            if (!string.IsNullOrWhiteSpace(cursorNorm))
            {
                if (!string.IsNullOrWhiteSpace(cursorId))
                {
                    usersQuery = usersQuery.Where(u =>
                        u.NormalizedUserName != null &&
                        (
                            u.NormalizedUserName.CompareTo(cursorNorm) > 0 ||
                            (u.NormalizedUserName == cursorNorm && u.Id.CompareTo(cursorId) > 0)
                        )
                    );
                }
                else
                {
                    // Backward-compatible fallback if you ever had old cursor formats
                    usersQuery = usersQuery.Where(u =>
                        u.NormalizedUserName != null &&
                        u.NormalizedUserName.CompareTo(cursorNorm) > 0);
                }
            }

            // Order consistently and take pageSize + 1 to detect HasMore
            var users = await usersQuery
                .OrderBy(u => u.NormalizedUserName)
                .ThenBy(u => u.Id)
                .Take(pageSize + 1)
                .ToListAsync();

            var hasMore = users.Count > pageSize;
            if (hasMore)
                users.RemoveAt(users.Count - 1);

            // Follow statuses for current user
            var userIds = users.Select(u => u.Id).ToList();
            var followStatuses = new Dictionary<string, string>();

            if (currentUserId != null && userIds.Count > 0)
            {
                var follows = await _db.UserFollows
                    .Where(f => f.ObserverId == currentUserId && userIds.Contains(f.TargetId))
                    .ToListAsync();

                foreach (var follow in follows)
                    followStatuses[follow.TargetId] = follow.Status.ToString();
            }

            model.Items = users.Select(u => new SearchProfileItemViewModel
            {
                UserId = u.Id,
                UserName = u.UserName ?? "",
                ProfileImage = u.ProfileImage,
                IsPrivate = u.IsPrivate,
                FollowStatus = followStatuses.TryGetValue(u.Id, out var status) ? status : "None"
            }).ToList();

            model.HasMore = hasMore;

            // Next cursor = last item's (NormalizedUserName|Id)
            if (hasMore && users.Count > 0)
            {
                var last = users[^1];
                // NormalizedUserName should exist, but guard anyway
                var lastNorm = last.NormalizedUserName ?? (last.UserName ?? "").ToUpperInvariant();
                model.NextCursor = $"{lastNorm}|{last.Id}";
            }
            else
            {
                model.NextCursor = null;
            }

            return PartialView("_SearchResultsPartial", model);
        }
    }
}
