using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Orders.Bll.Dtos;
using Orders.Bll.Services;
using Orders.Api.Metrics;
using Platform.ServiceDefaults;

namespace Orders.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/orders")]
public class OrdersController(IOrderService orders, ICustomerService customers, OrderMetrics metrics) : ControllerBase
{
    /// <summary>Замовлення клієнта: GET /api/orders?customerId=1</summary>
    [Authorize(Policy = "orders.read")]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> GetByCustomer(
        [FromQuery, Range(1, long.MaxValue)] long customerId, CancellationToken ct)
    {
        if (await IsForeignCustomerAsync(customerId, ct)) return Forbid();
        return Ok(await metrics.MeasureAsync("get_by_customer", () => orders.GetByCustomerAsync(customerId, ct)));
    }

    [Authorize(Policy = "orders.read")]
    [HttpGet("{id:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetById(long id, CancellationToken ct)
    {
        var order = await metrics.MeasureAsync("get_by_id", () => orders.GetByIdAsync(id, ct));
        if (await IsForeignCustomerAsync(order.CustomerId, ct)) return Forbid();
        return Ok(order);
    }

    [Authorize(Policy = "orders.write")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderDto input, CancellationToken ct)
    {
        var created = await metrics.MeasureAsync("create", () => orders.CreateAsync(input, ct));
        metrics.OrderCreated(created.Status);        // лічильник росте лише після успіху
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Підтвердження: статус -> StockConfirmed і зменшення залишку в одній транзакції.</summary>
    [Authorize(Policy = "orders.write")]
    [HttpPost("{id:long}/confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirm(long id, CancellationToken ct)
    {
        await metrics.MeasureAsync("confirm", () => orders.ConfirmOrderAsync(id, ct));
        metrics.OrderConfirmed();
        return NoContent();
    }

    /// <summary>Зміна статусу (Paid, Shipped, Cancelled...); недопустимий перехід дає 409.</summary>
    [Authorize(Policy = "orders.write")]
    [HttpPut("{id:long}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeStatus(long id, ChangeOrderStatusDto input, CancellationToken ct)
    {
        await metrics.MeasureAsync("change_status", () => orders.ChangeStatusAsync(id, input.Status, ct));
        metrics.StatusChanged(input.Status.ToString()!);
        return NoContent();
    }

    [Authorize(Policy = "orders.write")]
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await metrics.MeasureAsync("delete", () => orders.DeleteAsync(id, ct));
        return NoContent();
    }

    /// <summary>
    /// Користувач з роллю User (власний JWT, не Admin) бачить лише свої замовлення:
    /// email клієнта має збігатися з email у токені. Адміни та сервісні токени не обмежуються.
    /// </summary>
    private async Task<bool> IsForeignCustomerAsync(long customerId, CancellationToken ct)
    {
        if (!User.IsInRole("User") || User.IsInRole("Admin")) return false;
        var customer = await customers.GetByIdAsync(customerId, ct);
        return !string.Equals(customer.Email, User.GetEmail(), StringComparison.OrdinalIgnoreCase);
    }
}