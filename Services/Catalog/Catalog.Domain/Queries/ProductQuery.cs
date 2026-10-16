namespace Catalog.Domain.Queries;

/// <summary>Параметри запиту списку товарів: пагінація, фільтри, сортування.</summary>
public record ProductQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    int? CategoryId = null,
    string SortBy = "name",      // дозволені: name, price, stock
    string SortDir = "asc");     // asc | desc