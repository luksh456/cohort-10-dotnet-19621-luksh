using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Api.Enums;
using TaskManagement.Api.Models;

namespace TaskManagement.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAdminAsync(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        var email = configuration["SeedAdmin:Email"]?.Trim().ToLowerInvariant();
        var password = configuration["SeedAdmin:Password"];
        var name = configuration["SeedAdmin:Name"] ?? "Administrator";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Admin seed skipped: SeedAdmin:Email and SeedAdmin:Password are not configured.");
            return;
        }

        var context = services.GetRequiredService<AppDbContext>();

        if (await context.Users.AnyAsync(u => u.Role == UserRole.Admin))
        {
            return;
        }

        if (await context.Users.AnyAsync(u => u.Email == email))
        {
            logger.LogWarning("Admin seed skipped: a user with the configured email already exists.");
            return;
        }

        var hasher = services.GetRequiredService<IPasswordHasher<User>>();
        var admin = new User { Name = name, Email = email, Role = UserRole.Admin };
        admin.PasswordHash = hasher.HashPassword(admin, password);

        context.Users.Add(admin);

        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Another instance may have created the same user at the same time.
            if (await context.Users.AsNoTracking().AnyAsync(u => u.Email == email))
            {
                logger.LogInformation("Admin seed skipped: another instance already created the configured user.");
                return;
            }

            throw;
        }

        logger.LogInformation("Default admin user created.");
    }
}