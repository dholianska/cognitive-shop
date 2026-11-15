using Microsoft.AspNetCore.Identity;

namespace Auth.Api.Models;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<RefreshToken> RefreshTokens { get; set; } = [];
}