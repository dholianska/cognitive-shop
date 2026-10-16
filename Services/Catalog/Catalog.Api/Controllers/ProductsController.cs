using System.ComponentModel.DataAnnotations;
using Catalog.Bll.Dtos;
using Catalog.Bll.Services;
using Catalog.Domain.Queries;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

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
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetList(
        [FromQuery] ProductQuery query,
        [FromServices] IValidator<ProductQuery> validator,
        CancellationToken ct)
    {
        if (await ValidateAsync(validator, query, ct) is { } invalid) return invalid;

        var result = await service.GetListAsync(query, ct);
        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        return Ok(result);
    }

    /// <summary>Товари з малим залишком (друга специфікація).</summary>
    [HttpGet("low-stock")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
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
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductDto>> Create(
        CreateProductDto input,
        [FromServices] IValidator<CreateProductDto> validator,
        CancellationToken ct)
    {
        // явний виклик FluentValidation (автоматична інтеграція AddFluentValidation застаріла)
        if (await ValidateAsync(validator, input, ct) is { } invalid) return invalid;

        var created = await service.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);   // 201 + Location
    }

    // прості правила (Range) перевіряє DataAnnotations ще до виклику дії
    [HttpPut("{id:int}/price")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePrice(int id, ChangePriceDto input, CancellationToken ct)
    {
        await service.ChangePriceAsync(id, input.NewPrice, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/stock/add")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddStock(int id, StockQuantityDto input, CancellationToken ct)
    {
        await service.AddStockAsync(id, input.Quantity, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/stock/remove")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
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

    /// <summary>Викликає валідатор явно; якщо є помилки, повертає 400 у форматі ValidationProblemDetails.</summary>
    private async Task<ActionResult?> ValidateAsync<T>(IValidator<T> validator, T model, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(model, ct);
        return result.IsValid
            ? null
            : ValidationProblem(new ValidationProblemDetails(result.ToDictionary()));
    }
}