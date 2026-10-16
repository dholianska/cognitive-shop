using Catalog.Bll.Dtos;
using Catalog.Bll.Services;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController(ICategoryService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetAll(CancellationToken ct)
        => Ok(await service.GetAllAsync(ct));

    /// <summary>Категорія з товарами (Explicit Loading: у лозі видно 2 SQL-запити).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryWithProductsDto>> GetById(int id, CancellationToken ct)
        => Ok(await service.GetWithProductsAsync(id, ct));

    /// <summary>Товари категорії, додані після вказаної дати (M:N + поле AddedAt).</summary>
    [HttpGet("recent-products")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetRecentProducts(
        [FromQuery] string name, [FromQuery] DateTime since, CancellationToken ct)
        => Ok(await service.GetRecentlyAddedAsync(name, since, ct));
}