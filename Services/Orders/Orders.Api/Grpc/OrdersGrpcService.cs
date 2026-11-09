using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Orders.Bll.Dtos;
using Orders.Bll.Services;
using Orders.Domain.Exceptions;
using Orders.Grpc;
using Serilog.Context;
using System.Diagnostics;

namespace Orders.Api.GrpcServices;

/// <summary>gRPC-адаптер над IOrderService.</summary>
public class OrdersGrpcService(IOrderService orders) : OrdersService.OrdersServiceBase
{
    public override async Task<OrderReply> GetOrder(GetOrderRequest request, ServerCallContext context)
    {
        using var scope = LogContext.PushProperty("Protocol", "gRPC");
        Activity.Current?.SetTag("order.id", request.Id);

        if (request.Id <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Id must be positive"));

        try
        {
            var order = await orders.GetByIdAsync(request.Id, context.CancellationToken);
            return ToReply(order);
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (ValidationException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    private static OrderReply ToReply(OrderDto o)
    {
        var reply = new OrderReply
        {
            Id = o.Id,
            CustomerId = o.CustomerId,
            Status = o.Status,
            OrderDate = Timestamp.FromDateTime(DateTime.SpecifyKind(o.OrderDate, DateTimeKind.Utc))
        };
        if (o.DeliveryEmail is not null) reply.DeliveryEmail = o.DeliveryEmail;
        if (o.Comment is not null) reply.Comment = o.Comment;

        reply.Items.AddRange(o.Items.Select(i => new OrderItemReply
        {
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            UnitPrice = (double)i.UnitPrice,
            Discount = (double)i.Discount,
            Units = i.Units
        }));
        return reply;
    }
}