using System.Security.Claims;

namespace Platform.ServiceDefaults;

public static class PlatformClaimsExtensions
{
    public static string? GetUserId(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");

    public static string? GetEmail(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email");

    /// <summary>Scope з токена IdentityServer (claim "scope" може бути списком або рядком через пробіл).</summary>
    public static bool HasScope(this ClaimsPrincipal user, string scope)
        => user.FindAll("scope")
               .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
               .Contains(scope);

    public static bool IsIssuedBy(this ClaimsPrincipal user, string? issuer)
        => issuer is not null && user.FindFirstValue("iss") == issuer;

    /// <summary>Дозвіл дає scope (IdentityServer) або токен власного Auth Service (доступ за ролями).</summary>
    public static bool HasScopeOrOwnJwt(this ClaimsPrincipal user, string scope, string? ownIssuer)
        => user.HasScope(scope) || user.IsIssuedBy(ownIssuer);
}