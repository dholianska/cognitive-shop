using Microsoft.AspNetCore.Mvc;
using Platform.Aggregator.Models;
using Platform.Aggregator.Services;

namespace Platform.Aggregator.Controllers;

[ApiController]
[Route("api/aggregator")]
public class AggregatorController(
    CachedOrderAggregationService grpcCached,
    OrderAggregationService http) : ControllerBase
{
    /// <summary>Повна картка замовлення через gRPC (+ кеш Redis 30 с). ?noCache=true для вимірювань.</summary>
    [HttpGet("orders/{id:long}/full")]
    [ProducesResponseType<FullOrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public Task<ActionResult<FullOrderDto>> GetFullOrder(
        long id, [FromQuery] bool noCache = false, CancellationToken ct = default)
        => RunAsync(id, () => grpcCached.GetFullOrderAsync(id, useCache: !noCache, ct));

    /// <summary>Те саме через HTTP (Лаб. 05). Лишено для порівняння тривалості HTTP і gRPC.</summary>
    [HttpGet("orders/{id:long}/full/http")]
    public Task<ActionResult<FullOrderDto>> GetFullOrderHttp(long id, CancellationToken ct = default)
        => RunAsync(id, () => http.GetFullOrderAsync(id, ct));

    private async Task<ActionResult<FullOrderDto>> RunAsync(long id, Func<Task<FullOrderDto?>> call)
    {
        if (id <= 0)
            return BadRequest(new ProblemDetails { Status = 400, Title = "Validation failed", Detail = "Id must be positive" });

        try
        {
            var result = await call();
            return result is null
                ? NotFound(new ProblemDetails { Status = 404, Title = "Order not found", Detail = $"Order {id} not found" })
                : Ok(result);
        }
        catch (UpstreamUnavailableException ex)
        {
            return Problem(statusCode: 503, title: "Upstream service unavailable", detail: ex.Message);
        }
    }
}