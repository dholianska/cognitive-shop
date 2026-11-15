using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Platform.ServiceDefaults;

/// <summary>Назви схем автентифікації платформи та вибір схеми за issuer токена.</summary>
public static class AuthSchemes
{
    public const string Multi = "Multi";                   // політика-схема, сама нічого не валідує
    public const string OwnJwt = "OwnJwt";                 // Ч.1: JWT власного Auth Service
    public const string IdentityServer = "IdentityServer"; // Ч.2: токени Duende IdentityServer

    public static string SelectByIssuer(HttpContext context, IConfiguration config)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return OwnJwt;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var token = header["Bearer ".Length..].Trim();
            if (!handler.CanReadToken(token)) return OwnJwt;

            var issuer = handler.ReadJwtToken(token).Issuer?.TrimEnd('/');
            var identityServer = config["IdentityServer:Authority"]?.TrimEnd('/');
            return identityServer is not null && issuer == identityServer ? IdentityServer : OwnJwt;
        }
        catch (ArgumentException)
        {
            return OwnJwt;   // зіпсований токен відхилить сама схема
        }
    }

    /// <summary>Параметри валідації власного JWT: однакові issuer/audience/ключ у всіх сервісах (передає AppHost).</summary>
    public static TokenValidationParameters OwnJwtParameters(IConfiguration config)
    {
        var key = config["Jwt:SecretKey"] ?? throw new InvalidOperationException("Jwt:SecretKey is missing");
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = config["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = config["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    }
}