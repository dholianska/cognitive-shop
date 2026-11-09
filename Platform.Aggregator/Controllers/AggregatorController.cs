using Microsoft.AspNetCore.Mvc;
using Platform.Aggregator.Models;
using Platform.Aggregator.Services;

namespace Platform.Aggregator.Controllers;

[ApiController]
[Route("api/aggregator")]
public class AggregatorController(OrderAggregationService service) : ControllerBase
{
    /// <summary>Повна картка замовлення: замовлення + товари + відгуки (часткова відповідь при збої).</summary>
    [HttpGet("orders/{id:long}/full")]
    [ProducesResponseType<FullOrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<FullOrderDto>> GetFullOrder(long id, CancellationToken ct)
    {
        try
        {
            var result = await service.GetFullOrderAsync(id, ct);
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