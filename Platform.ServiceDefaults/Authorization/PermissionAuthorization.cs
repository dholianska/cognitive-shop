using System.Text.Json;
using Microsoft.AspNetCore.Authorization;

namespace Platform.ServiceDefaults.Authorization;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>[RequirePermission("orders:create")]: політика називається так само, як permission.</summary>
public sealed class RequirePermissionAttribute(string permission) : AuthorizeAttribute(permission)
{
    public string Permission { get; } = permission;
}

/// <summary>
/// Рішення «пропустити чи відхилити» приймає сам мікросервіс, без звернення до Keycloak на кожен запит.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    // Права звичайного користувача власного Auth Service (Ч.1), узгоджені з роллю user з таблиці 7
    private static readonly HashSet<string> OwnUserPermissions = new() { "orders:read", "orders:create", "products:read" };

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var user = context.User;
        var permission = requirement.Permission;

        // 1) Keycloak: claim authorization.permissions[].scopes[] (RPT)
        var authClaim = user.FindFirst("authorization")?.Value;
        if (authClaim is not null)
        {
            if (HasKeycloakPermission(authClaim, permission)) context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // 2) IdentityServer (Ч.2): permission orders:read відповідає scope orders.read, будь-яка інша дія scope orders.write
        if (user.HasScope(ToScope(permission)))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // 3) Власний JWT (Ч.1): Admin має все, User лише базові права
        if (user.IsInRole("Admin") || (user.IsInRole("User") && OwnUserPermissions.Contains(permission)))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }

    private static bool HasKeycloakPermission(string json, string permission)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("permissions", out var permissions)
                && permissions.EnumerateArray().Any(p =>
                    p.TryGetProperty("scopes", out var scopes)
                    && scopes.EnumerateArray().Any(s => s.GetString() == permission));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string ToScope(string permission)
    {
        var parts = permission.Split(':', 2);
        return parts.Length == 2 ? $"{parts[0]}.{(parts[1] == "read" ? "read" : "write")}" : permission;
    }
}