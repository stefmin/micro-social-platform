using Microsoft.AspNetCore.Identity;
using MicroSocialPlatform.Models;
public static class DebugReset
{
    public static async Task Run(WebApplication app)
    {
        // Create a scope to get the UserManager service
        using (var scope = app.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // EDIT THESE VARIABLES
            string email = "TEOMINEA@TEOMINEA.COM";
            string newPassword = "Parola0)";

            var user = await userManager.FindByEmailAsync(email);
            if (user != null)
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                var result = await userManager.ResetPasswordAsync(user, token, newPassword);

                if (result.Succeeded)
                {
                    Console.BackgroundColor = ConsoleColor.Green;
                    Console.ForegroundColor = ConsoleColor.Black;
                    Console.WriteLine($"\nSUCCESS: Password for {email} reset to {newPassword}\n");
                    Console.ResetColor();
                }
                else
                {
                    Console.BackgroundColor = ConsoleColor.Red;
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"\nERROR: {string.Join(", ", result.Errors.Select(e => e.Description))}\n");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.WriteLine($"\nWARNING: User {email} not found.\n");
            }
        }
    }
}