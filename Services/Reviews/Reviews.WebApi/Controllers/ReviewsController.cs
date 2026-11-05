using MediatR;
using Microsoft.AspNetCore.Mvc;
using Reviews.Application.Commands.AddComment;
using Reviews.Application.Commands.CreateReview;
using Reviews.Application.Commands.DeleteReview;
using Reviews.Application.Commands.UpdateReview;
using Reviews.Application.Common.Models;
using Reviews.Application.Queries.GetReviewById;
using Reviews.Application.Queries.GetReviewsList;
using Reviews.WebApi.Contracts;

namespace Reviews.WebApi.Controllers;

/// <summary>Тонкий адаптер: усі операції виконує MediatR (ISender), тут немає ні бізнес-правил, ні MongoDB.</summary>
[ApiController]
[Route("api/reviews")]
public class ReviewsController(ISender mediator) : ControllerBase
{
    /// <summary>Список відгуків: GET /api/reviews?productId=1&amp;page=1&amp;pageSize=10 (фільтр і пагінація в MongoDB).</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ReviewDto>>> GetList(
        [FromQuery] long? productId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await mediator.Send(new GetReviewsListQuery(productId, page, pageSize), ct));

    /// <summary>Відгук за Id. Версія документа повертається в заголовку ETag (для If-Match).</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewDto>> GetById(string id, CancellationToken ct)
    {
        var review = await mediator.Send(new GetReviewByIdQuery(id), ct);
        Response.Headers.ETag = $"\"{review.Version}\"";
        return Ok(review);
    }

    /// <summary>Створення відгуку. 201 із Location, де в адресі рядковий ObjectId.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<string>> Create(CreateReviewCommand command, CancellationToken ct)
    {
        var id = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    /// <summary>
    /// Оновлення відгуку. Заголовок If-Match (версія з ETag) необов'язковий: якщо він є і документ
    /// уже змінили, повертається 409.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewDto>> Update(
        string id, UpdateReviewRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken ct)
    {
        int? expectedVersion = null;
        if (!string.IsNullOrWhiteSpace(ifMatch))
        {
            if (!int.TryParse(ifMatch.Trim().Trim('"'), out var version) || version < 0)
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid If-Match header",
                    detail: "If-Match must contain the document version, for example \"3\"");
            expectedVersion = version;
        }

        var updated = await mediator.Send(
            new UpdateReviewCommand(id, request.Rating, request.Text, expectedVersion), ct);

        Response.Headers.ETag = $"\"{updated.Version}\"";
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await mediator.Send(new DeleteReviewCommand(id), ct);
        return NoContent();
    }

    /// <summary>Додає коментар у вкладений масив відгуку.</summary>
    [HttpPost("{id}/comments")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddComment(string id, AddCommentRequest request, CancellationToken ct)
    {
        await mediator.Send(new AddCommentCommand(id, request.AuthorId, request.AuthorName, request.Text), ct);
        return NoContent();
    }
}