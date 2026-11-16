using System.Buffers.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Auth.Api.Models;
using Auth.Api.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Auth.Api.Services;

public sealed class JwtTokenService(IOptions<JwtSettings> options) : IJwtTokenService
{
    private readonly JwtSettings _settings = options.Value;

    private SymmetricSecurityKey Key => new(Encoding.UTF8.GetBytes(_settings.SecretKey));

    public string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName!),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes),
            signingCredentials: new SigningCredentials(Key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
        => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(64));

    public ClaimsPrincipal? ValidateToken(string token, bool validateLifetime = true)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _settings.Issuer,
            ValidateAudience = true,
            ValidAudience = _settings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = Key,
            ValidateLifetime = validateLifetime,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, parameters, out var validated);
            return validated is JwtSecurityToken jwt && jwt.Header.Alg == SecurityAlgorithms.HmacSha256
                ? principal
                : null;
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }

    public string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}