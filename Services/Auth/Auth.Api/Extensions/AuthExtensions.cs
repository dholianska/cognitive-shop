using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Auth.Api.Extensions;

public static class AuthExtensions
{
    /// <summary>Id користувача з токена: claim sub (після мапінгу це NameIdentifier).</summary>
    public static string? GetUserId(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");

    /// <summary>Помилки Identity у вигляді 400 ValidationProblemDetails.</summary>
    public static ActionResult IdentityProblem(this ControllerBase controller, IdentityResult result)
    {
        var errors = result.Errors
            .GroupBy(e => e.Code)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
        return controller.ValidationProblem(new ValidationProblemDetails(errors));
    }
}