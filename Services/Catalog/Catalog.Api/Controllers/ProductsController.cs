using Catalog.Bll.Dtos;
using Catalog.Bll.Services;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Catalog.Domain.Queries;

namespace Catalog.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(IProductService service) : ControllerBase
{
    /// <summary>
    /// Список товарів: GET /api/products?page=1&amp;pageSize=10&amp;search=тест&amp;minPrice=50&amp;sortBy=price&amp;sortDir=desc.
    /// Пагінація, фільтри й сортування виконуються на рівні SQL. Метадані в тілі відповіді
    /// (items, totalCount, page, pageSize, totalPages, hasNext, hasPrevious)
    /// і дублюються в заголовку X-Total-Count.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetList(
        [FromQuery] ProductQuery query, CancellationToken ct)
    {
        var result = await service.GetListAsync(query, ct);
        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        return Ok(result);
    }

    /// <summary>Товари з малим залишком (друга специфікація).</summary>
    [HttpGet("low-stock")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetLowStock(
        [FromQuery, Range(0, 100000)] int threshold = 10, CancellationToken ct = default)
        => Ok(await service.GetLowStockAsync(threshold, ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductCardDto>> GetById(int id, CancellationToken ct)
        => Ok(await service.GetByIdAsync(id, ct));   // не знайдено: 404 (обробник)

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductDto>> Create(CreateProductDto input, CancellationToken ct)
    {
        var created = await service.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);   // 201 + Location
    }

    [HttpPut("{id:int}/price")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePrice(int id, ChangePriceDto input, CancellationToken ct)
    {
        await service.ChangePriceAsync(id, input.NewPrice, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/stock/add")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddStock(int id, StockQuantityDto input, CancellationToken ct)
    {
        await service.AddStockAsync(id, input.Quantity, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/stock/remove")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveStock(int id, StockQuantityDto input, CancellationToken ct)
    {
        await service.RemoveStockAsync(id, input.Quantity, ct);
        return NoContent();
    }

    [HttpPut("{id:int}/categories/{categoryId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignCategory(int id, int categoryId, CancellationToken ct)
    {
        await service.AssignCategoryAsync(id, categoryId, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}