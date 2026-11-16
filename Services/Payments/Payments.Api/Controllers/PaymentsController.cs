using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.ServiceDefaults.Authorization;

namespace Payments.Api.Controllers;

/// <summary>Демо-сервіс платежів: ендпоінти-заглушки для перевірки доступу. Доступ лише в admin.</summary>
[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController : ControllerBase
{
    [HttpGet]
    [RequirePermission("payments:read")]
    public IActionResult GetAll() => Ok(new[]
    {
        new { Id = 1, Amount = 100.00m, Status = "Completed" },
        new { Id = 2, Amount = 45.50m, Status = "Pending" }
    });

    [HttpPost("process")]
    [RequirePermission("payments:process")]
    public IActionResult Process() => Accepted(new { Message = "Payment processed (stub)" });

    [HttpPost("refund")]
    [RequirePermission("payments:refund")]
    public IActionResult Refund() => Accepted(new { Message = "Payment refunded (stub)" });
}