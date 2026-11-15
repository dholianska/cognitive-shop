using System.Net.Http.Headers;

namespace Platform.Aggregator.Clients;

/// <summary>Лаб. 09, Ч.1: пересилає Authorization користувача в downstream-сервіси (у Ч.2 замінимо на токен самого сервісу).</summary>
public sealed class BearerForwardingHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var header = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(header) && AuthenticationHeaderValue.TryParse(header, out var value))
            request.Headers.Authorization = value;
        return base.SendAsync(request, ct);
    }
}