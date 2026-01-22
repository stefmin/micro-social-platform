using MicroSocialPlatform.Models;
using Microsoft.AspNetCore.Identity;

namespace MicroSocialPlatform.Middleware
{
  public class BannedUserMiddleware
  {
    private readonly RequestDelegate _next;

    public BannedUserMiddleware(RequestDelegate next)
    {
      _next = next;
    }

    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
      if (context.User.Identity?.IsAuthenticated == true)
      {
        var user = await userManager.GetUserAsync(context.User);
        if (user?.Status == UserStatus.Banned)
        {
          await signInManager.SignOutAsync();
          context.Response.Redirect("/Identity/Account/Banned");
          return;
        }
      }

      await _next(context);
    }
  }

  public static class BannedUserMiddlewareExtensions
  {
    public static IApplicationBuilder UseBannedUserCheck(this IApplicationBuilder builder)
    {
      return builder.UseMiddleware<BannedUserMiddleware>();
    }
  }
}
