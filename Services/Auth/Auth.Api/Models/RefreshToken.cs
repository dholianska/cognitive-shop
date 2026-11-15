namespace Auth.Api.Models;

public class RefreshToken
{
    public int Id { get; set; }

    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;

    /// <summary>SHA-256 хеш токена (сам токен в БД не зберігається).</summary>
    public string TokenHash { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Заповнено, якщо токен відкликано (logout, revoke, rotation, reset пароля).</summary>
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>Хеш токена, який замінив цей при rotation.</summary>
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;
}