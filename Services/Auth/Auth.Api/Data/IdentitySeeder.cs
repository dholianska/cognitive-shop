using Auth.Api.Constants;
using Auth.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Data;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, CancellationToken ct = default)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync(ct);

        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { AppRoles.User, AppRoles.Admin })
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));

        // Тестовий адмін (лише якщо задано в конфігурації; у Development — appsettings.Development.json)
        var email = config["Seed:AdminEmail"];
        var password = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync(email) is not null) return;

        var admin = new ApplicationUser { UserName = "admin", Email = email, EmailConfirmed = true };
        var result = await users.CreateAsync(admin, password);
        if (result.Succeeded)
            await users.AddToRolesAsync(admin, [AppRoles.User, AppRoles.Admin]);
    }
}