using System.ComponentModel.DataAnnotations;
using Catalog.Bll.Dtos;
using Catalog.Bll.Services;
using Catalog.Domain.Queries;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Catalog.Api.Metrics;

namespace Catalog.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(IProductService service, CatalogMetrics metrics) : ControllerBase
{
    /// <summary>
    /// Список товарів: GET /api/products?page=1&amp;pageSize=10&amp;search=тест&amp;minPrice=50&amp;sortBy=price&amp;sortDir=desc.
    /// Пагінація, фільтри й сортування виконуються на рівні SQL. Метадані в тілі відповіді
    /// (items, totalCount, page, pageSize, totalPages, hasNext, hasPrevious)
    /// і дублюються в заголовку X-Total-Count.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetList(
        [FromQuery] ProductQuery query,
        [FromServices] IValidator<ProductQuery> validator,
        CancellationToken ct)
    {
        if (await ValidateAsync(validator, query, ct) is { } invalid) return invalid;

        var result = await metrics.MeasureAsync("list", () => service.GetListAsync(query, ct));
        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        return Ok(result);
    }

    /// <summary>Товари з малим залишком (друга специфікація).</summary>
    [HttpGet("low-stock")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetLowStock(
        [FromQuery, Range(0, 100000)] int threshold = 10, CancellationToken ct = default)
        => Ok(await metrics.MeasureAsync("low_stock", () => service.GetLowStockAsync(threshold, ct)));

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductCardDto>> GetById(int id, CancellationToken ct)
        => (await metrics.MeasureAsync("get_by_id", () => service.GetByIdAsync(id, ct))) is { } dto
            ? Ok(dto)
            : NotFound();   // не знайдено: 404 (обробник)

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductDto>> Create(
        CreateProductDto input,
        [FromServices] IValidator<CreateProductDto> validator,
        CancellationToken ct)
    {
        // явний виклик FluentValidation (автоматична інтеграція AddFluentValidation застаріла)
        if (await ValidateAsync(validator, input, ct) is { } invalid) return invalid;

        var created = await metrics.MeasureAsync("create", () => service.CreateAsync(input, ct));
        metrics.ProductCreated();
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);   // 201 + Location
    }

    // прості правила (Range) перевіряє DataAnnotations ще до виклику дії
    [HttpPut("{id:int}/price")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePrice(int id, ChangePriceDto input, CancellationToken ct)
    {
        await metrics.MeasureAsync("change_price", () => service.ChangePriceAsync(id, input.NewPrice, ct));
        metrics.ProductUpdated("price");
        return NoContent();
    }

    [HttpPost("{id:int}/stock/add")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddStock(int id, StockQuantityDto input, CancellationToken ct)
    {
        await metrics.MeasureAsync("add_stock", () => service.AddStockAsync(id, input.Quantity, ct));
        metrics.ProductUpdated("stock_added");
        return NoContent();
    }

    [HttpPost("{id:int}/stock/remove")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveStock(int id, StockQuantityDto input, CancellationToken ct)
    {
        await metrics.MeasureAsync("remove_stock", () => service.RemoveStockAsync(id, input.Quantity, ct));
        metrics.ProductUpdated("stock_removed");
        return NoContent();
    }

    [HttpPut("{id:int}/categories/{categoryId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignCategory(int id, int categoryId, CancellationToken ct)
    {
        await metrics.MeasureAsync("assign_category", () => service.AssignCategoryAsync(id, categoryId, ct));
        metrics.ProductUpdated("category_assigned");
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await metrics.MeasureAsync("delete", () => service.DeleteAsync(id, ct));
        metrics.ProductDeleted();
        return NoContent();
    }

    /// <summary>Викликає валідатор явно; якщо є помилки, повертає 400 у форматі ValidationProblemDetails.</summary>
    private async Task<ActionResult?> ValidateAsync<T>(IValidator<T> validator, T model, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(model, ct);
        return result.IsValid
            ? null
            : ValidationProblem(new ValidationProblemDetails(result.ToDictionary()));
    }
}