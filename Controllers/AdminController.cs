using MicroSocialPlatform.Data;
using MicroSocialPlatform.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroSocialPlatform.Controllers
{
  [Authorize(Roles = "Admin")]
  public class AdminController : Controller
  {
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
      _db = db;
      _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search)
    {
      var currentUserId = _userManager.GetUserId(User);

      var query = _db.Users.AsQueryable();

      if (!string.IsNullOrWhiteSpace(search))
      {
        query = query.Where(u =>
            u.UserName.Contains(search) ||
            u.Email.Contains(search) ||
            u.FullName.Contains(search));
      }

      var users = await query
          .Where(u => u.Id != currentUserId)
          .OrderBy(u => u.UserName)
          .ToListAsync();

      ViewBag.Search = search;
      return View(users);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuspendUser(string id)
    {
      var currentUserId = _userManager.GetUserId(User);
      if (id == currentUserId)
      {
        TempData["Error"] = "You cannot suspend yourself.";
        return RedirectToAction(nameof(Index));
      }

      var user = await _userManager.FindByIdAsync(id);
      if (user == null) return NotFound();

      if (await _userManager.IsInRoleAsync(user, "Admin"))
      {
        TempData["Error"] = "You cannot suspend another admin.";
        return RedirectToAction(nameof(Index));
      }

      user.Status = UserStatus.Suspended;
      await _db.SaveChangesAsync();

      TempData["Success"] = $"User {user.UserName} has been suspended.";
      return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BanUser(string id)
    {
      var currentUserId = _userManager.GetUserId(User);
      if (id == currentUserId)
      {
        TempData["Error"] = "You cannot ban yourself.";
        return RedirectToAction(nameof(Index));
      }

      var user = await _userManager.FindByIdAsync(id);
      if (user == null) return NotFound();

      if (await _userManager.IsInRoleAsync(user, "Admin"))
      {
        TempData["Error"] = "You cannot ban another admin.";
        return RedirectToAction(nameof(Index));
      }

      user.Status = UserStatus.Banned;
      await _db.SaveChangesAsync();

      TempData["Success"] = $"User {user.UserName} has been banned.";
      return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnsuspendUser(string id)
    {
      var user = await _userManager.FindByIdAsync(id);
      if (user == null) return NotFound();

      user.Status = UserStatus.Active;
      await _db.SaveChangesAsync();

      TempData["Success"] = $"User {user.UserName} has been unsuspended.";
      return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnbanUser(string id)
    {
      var user = await _userManager.FindByIdAsync(id);
      if (user == null) return NotFound();

      user.Status = UserStatus.Active;
      await _db.SaveChangesAsync();

      TempData["Success"] = $"User {user.UserName} has been unbanned.";
      return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePost(int id, string? returnUrl)
    {
      var post = await _db.Post.FirstOrDefaultAsync(p => p.Id == id);
      if (post == null) return NotFound();

      _db.Post.Remove(post);
      await _db.SaveChangesAsync();

      TempData["Success"] = "Post has been deleted.";

      if (!string.IsNullOrEmpty(returnUrl))
        return LocalRedirect(returnUrl);

      return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(int id, string? returnUrl)
    {
      var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == id);
      if (comment == null) return NotFound();

      _db.Comments.Remove(comment);
      await _db.SaveChangesAsync();

      TempData["Success"] = "Comment has been deleted.";

      if (!string.IsNullOrEmpty(returnUrl))
        return LocalRedirect(returnUrl);

      return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGroup(int id)
    {
      var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == id);
      if (group == null) return NotFound();

      _db.Groups.Remove(group);
      await _db.SaveChangesAsync();

      TempData["Success"] = "Group has been deleted.";
      return RedirectToAction("Index", "Group");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PromoteToAdmin(string id)
    {
      var currentUserId = _userManager.GetUserId(User);
      if (id == currentUserId)
      {
        TempData["Error"] = "You are already an admin.";
        return RedirectToAction(nameof(Index));
      }

      var user = await _userManager.FindByIdAsync(id);
      if (user == null) return NotFound();

      if (await _userManager.IsInRoleAsync(user, "Admin"))
      {
        TempData["Error"] = $"{user.UserName} is already an admin.";
        return RedirectToAction(nameof(Index));
      }

      var result = await _userManager.AddToRoleAsync(user, "Admin");
      if (result.Succeeded)
      {
        TempData["Success"] = $"{user.UserName} has been promoted to Admin.";
      }
      else
      {
        TempData["Error"] = $"Failed to promote {user.UserName} to Admin.";
      }

      return RedirectToAction(nameof(Index));
    }
  }
}
