using System.Text.Json.Serialization;

namespace Platform.Aggregator.Clients;

/// <summary>Лаб. 09, Ч.2: токен самого Aggregator за Client Credentials Flow (POST /connect/token), з кешуванням.</summary>
public sealed class ServiceTokenProvider(
    IHttpClientFactory httpFactory, IConfiguration config, ILogger<ServiceTokenProvider> logger)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTime _expiresUtc;

    public async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_token is not null && DateTime.UtcNow < _expiresUtc) return _token;

        await _lock.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTime.UtcNow < _expiresUtc) return _token;

            var authority = config["IdentityServer:Authority"]!.TrimEnd('/');
            var http = httpFactory.CreateClient("identityserver");
            using var response = await http.PostAsync($"{authority}/connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = config["IdentityServer:ClientId"]!,
                    ["client_secret"] = config["IdentityServer:ClientSecret"]!,
                    ["scope"] = config["IdentityServer:Scope"]!
                }), ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<TokenResponse>(ct)
                ?? throw new InvalidOperationException("Empty token response");
            _token = body.AccessToken;
            _expiresUtc = DateTime.UtcNow.AddSeconds(body.ExpiresIn - 30);   // невеликий запас
            logger.LogInformation("Obtained service token from IdentityServer, valid {Seconds} s", body.ExpiresIn);
            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}