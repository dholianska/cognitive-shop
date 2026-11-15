using Orders.Dal.Abstractions;

namespace Orders.Api.IntegrationEvents;

public sealed class ProductCreatedHandler(IUnitOfWork uow, ILogger<ProductCreatedHandler> logger)
    : IdempotentEventHandler<ProductCreatedEvent>(uow, logger)
{
    protected override long ProductIdOf(ProductCreatedEvent e) => e.ProductId;

    protected override void Validate(ProductCreatedEvent e)
        => Require(e.ProductId > 0 && !string.IsNullOrWhiteSpace(e.Name) && e.Price > 0 && e.AvailableStock >= 0,
            $"ProductCreatedEvent {e.EventId} has invalid data");

    protected override Task<bool> ApplyAsync(ProductCreatedEvent e, CancellationToken ct)
        => Uow.Products.UpsertSnapshotAsync(e.ProductId, e.Name, e.Price, e.AvailableStock, e.OccurredAt, ct);
}

public sealed class ProductPriceChangedHandler(IUnitOfWork uow, ILogger<ProductPriceChangedHandler> logger)
    : IdempotentEventHandler<ProductPriceChangedEvent>(uow, logger)
{
    protected override long ProductIdOf(ProductPriceChangedEvent e) => e.ProductId;

    protected override void Validate(ProductPriceChangedEvent e)
        => Require(e.ProductId > 0 && !string.IsNullOrWhiteSpace(e.ProductName) && e.NewPrice > 0,
            $"ProductPriceChangedEvent {e.EventId} has invalid data");

    // подія самодостатня: містить і назву, і нову ціну (повний зріз)
    protected override Task<bool> ApplyAsync(ProductPriceChangedEvent e, CancellationToken ct)
        => Uow.Products.UpsertSnapshotAsync(e.ProductId, e.ProductName, e.NewPrice, 0, e.OccurredAt, ct);
}

public sealed class ProductRenamedHandler(IUnitOfWork uow, ILogger<ProductRenamedHandler> logger)
    : IdempotentEventHandler<ProductRenamedEvent>(uow, logger)
{
    protected override long ProductIdOf(ProductRenamedEvent e) => e.ProductId;

    protected override void Validate(ProductRenamedEvent e)
        => Require(e.ProductId > 0 && !string.IsNullOrWhiteSpace(e.NewName) && e.Price > 0,
            $"ProductRenamedEvent {e.EventId} has invalid data");

    protected override Task<bool> ApplyAsync(ProductRenamedEvent e, CancellationToken ct)
        => Uow.Products.UpsertSnapshotAsync(e.ProductId, e.NewName, e.Price, 0, e.OccurredAt, ct);
}

public sealed class ProductDeletedHandler(IUnitOfWork uow, ILogger<ProductDeletedHandler> logger)
    : IdempotentEventHandler<ProductDeletedEvent>(uow, logger)
{
    protected override long ProductIdOf(ProductDeletedEvent e) => e.ProductId;

    protected override void Validate(ProductDeletedEvent e)
        => Require(e.ProductId > 0, $"ProductDeletedEvent {e.EventId} has invalid data");

    // м'яке видалення: товар лишається в історичних замовленнях
    protected override Task<bool> ApplyAsync(ProductDeletedEvent e, CancellationToken ct)
        => Uow.Products.MarkDeletedAsync(e.ProductId, e.OccurredAt, ct);
}