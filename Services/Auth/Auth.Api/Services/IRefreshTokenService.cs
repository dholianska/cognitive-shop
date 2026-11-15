using Auth.Api.Models;

namespace Auth.Api.Services;

public sealed record RotationResult(ApplicationUser User, string RefreshToken);

public interface IRefreshTokenService
{
    /// <summary>Створює новий refresh token (7 днів) і повертає його «сирим» значенням.</summary>
    Task<string> IssueAsync(ApplicationUser user, CancellationToken ct = default);

    /// <summary>Rotation: старий токен відкликається, створюється новий. null, якщо токен недійсний.</summary>
    Task<RotationResult?> RotateAsync(string rawToken, CancellationToken ct = default);

    /// <summary>Відкликає токен, що належить користувачу. false, якщо не знайдено або вже відкликано.</summary>
    Task<bool> RevokeAsync(string rawToken, string userId, CancellationToken ct = default);

    /// <summary>Відкликає всі активні токени користувача (скидання/зміна пароля).</summary>
    Task RevokeAllAsync(string userId, CancellationToken ct = default);
}