using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Platform.IdentityServer.Data;
using Platform.IdentityServer.Models;

namespace Platform.IdentityServer;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await services.GetRequiredService<PersistedGrantDbContext>().Database.MigrateAsync();
        var configuration = services.GetRequiredService<ConfigurationDbContext>();
        await configuration.Database.MigrateAsync();
        await services.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();

        await SeedConfigurationAsync(configuration);
        await SeedUsersAsync(services.GetRequiredService<UserManager<ApplicationUser>>());
    }

    // Додаємо лише відсутнє: після зміни Config.cs очисти БД (volume identityserver-sql) або онови записи вручну
    private static async Task SeedConfigurationAsync(ConfigurationDbContext db)
    {
        foreach (var r in Config.IdentityResources)
            if (!await db.IdentityResources.AnyAsync(x => x.Name == r.Name))
                db.IdentityResources.Add(r.ToEntity());

        foreach (var s in Config.ApiScopes)
            if (!await db.ApiScopes.AnyAsync(x => x.Name == s.Name))
                db.ApiScopes.Add(s.ToEntity());

        foreach (var r in Config.ApiResources)
            if (!await db.ApiResources.AnyAsync(x => x.Name == r.Name))
                db.ApiResources.Add(r.ToEntity());

        foreach (var c in Config.Clients)
            if (!await db.Clients.AnyAsync(x => x.ClientId == c.ClientId))
                db.Clients.Add(c.ToEntity());

        await db.SaveChangesAsync();
    }

    private static async Task SeedUsersAsync(UserManager<ApplicationUser> users)
    {
        foreach (var (name, email) in new[] { ("alice", "alice@example.com"), ("bob", "bob@example.com") })
        {
            if (await users.FindByNameAsync(name) is not null) continue;

            var result = await users.CreateAsync(
                new ApplicationUser { UserName = name, Email = email, EmailConfirmed = true }, "Pass123$");
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}