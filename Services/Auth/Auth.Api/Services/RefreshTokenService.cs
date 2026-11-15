using Auth.Api.Data;
using Auth.Api.Models;
using Auth.Api.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Auth.Api.Services;

public sealed class RefreshTokenService(
    ApplicationDbContext db,
    IJwtTokenService jwt,
    IOptions<JwtSettings> settings) : IRefreshTokenService
{
    public async Task<string> IssueAsync(ApplicationUser user, CancellationToken ct = default)
    {
        var raw = jwt.GenerateRefreshToken();
        db.RefreshTokens.Add(Create(user.Id, raw));
        await db.SaveChangesAsync(ct);
        return raw;
    }

    public async Task<RotationResult?> RotateAsync(string rawToken, CancellationToken ct = default)
    {
        var hash = jwt.HashToken(rawToken);
        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null) return null;

        // Повторне використання вже відкликаного токена = ознака крадіжки: відкликаємо всі сесії користувача
        if (stored.RevokedAtUtc is not null)
        {
            await RevokeAllAsync(stored.UserId, ct);
            return null;
        }

        if (stored.ExpiresAtUtc <= DateTime.UtcNow) return null;

        var newRaw = jwt.GenerateRefreshToken();
        var replacement = Create(stored.UserId, newRaw);

        stored.RevokedAtUtc = DateTime.UtcNow;                 // rotation: старий відкликається
        stored.ReplacedByTokenHash = replacement.TokenHash;
        db.RefreshTokens.Add(replacement);
        await db.SaveChangesAsync(ct);

        return new RotationResult(stored.User, newRaw);
    }

    public async Task<bool> RevokeAsync(string rawToken, string userId, CancellationToken ct = default)
    {
        var hash = jwt.HashToken(rawToken);
        var stored = await db.RefreshTokens
            .SingleOrDefaultAsync(t => t.TokenHash == hash && t.UserId == userId, ct);

        if (stored is null || stored.RevokedAtUtc is not null) return false;

        stored.RevokedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public Task RevokeAllAsync(string userId, CancellationToken ct = default)
        => db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, DateTime.UtcNow), ct);

    private RefreshToken Create(string userId, string raw)
    {
        var now = DateTime.UtcNow;
        return new RefreshToken
        {
            UserId = userId,
            TokenHash = jwt.HashToken(raw),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(settings.Value.RefreshTokenDays)
        };
    }
}