using FluentValidation;
using Grpc.Core;
using MediatR;
using Reviews.Application.Common.Models;
using Reviews.Application.Queries.GetReviewsList;
using Reviews.Domain.Exceptions;
using Reviews.Grpc;
using Serilog.Context;

namespace Reviews.WebApi.GrpcServices;

/// <summary>gRPC-адаптер над MediatR-запитами Application.</summary>
public class ReviewsGrpcService(ISender mediator) : ReviewsService.ReviewsServiceBase
{
    private const int MaxProducts = 100;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public override async Task<ReviewsReply> GetForProducts(GetForProductsRequest request, ServerCallContext context)
    {
        using var scope = LogContext.PushProperty("Protocol", "gRPC");

        if (request.ProductIds.Count is 0 or > MaxProducts)
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"ProductIds count must be 1..{MaxProducts}"));
        if (request.ProductIds.Any(id => id <= 0))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Every product id must be positive"));

        var pageSize = request.PageSize is > 0 and <= MaxPageSize ? request.PageSize : DefaultPageSize;

        try
        {
            var pages = await Task.WhenAll(request.ProductIds.Distinct().Select(id =>
                mediator.Send(new GetReviewsListQuery(id, 1, pageSize), context.CancellationToken)));

            var reply = new ReviewsReply();
            reply.Reviews.AddRange(pages.SelectMany(p => p.Items).Select(ToReply));
            return reply;
        }
        catch (ValidationException ex)   // FluentValidation з ValidationBehavior
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument,
                string.Join("; ", ex.Errors.Select(e => e.ErrorMessage).Distinct())));
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
    }

    private static ReviewReply ToReply(ReviewDto r) => new()
    {
        Id = r.Id,
        ProductId = r.ProductId,
        ProductName = r.ProductName,
        AuthorName = r.AuthorName,
        Rating = r.Rating,
        Text = r.Text,
        CommentsCount = r.CommentsCount
    };
}