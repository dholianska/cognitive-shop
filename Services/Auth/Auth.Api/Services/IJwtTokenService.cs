using System.Security.Claims;
using Auth.Api.Models;

namespace Auth.Api.Services;

public interface IJwtTokenService
{
    /// <summary>Короткоживучий access token (15 хв) з claims sub, unique_name, email, ролі.</summary>
    string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles);

    /// <summary>Криптостійкий випадковий refresh token (віддається клієнту як є, в БД лежить лише хеш).</summary>
    string GenerateRefreshToken();

    /// <summary>Перевіряє підпис, issuer, audience (і за потреби строк дії). null, якщо токен недійсний.</summary>
    ClaimsPrincipal? ValidateToken(string token, bool validateLifetime = true);

    /// <summary>SHA-256 хеш токена для зберігання в БД.</summary>
    string HashToken(string token);
}