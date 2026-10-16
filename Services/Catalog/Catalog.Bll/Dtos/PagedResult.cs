namespace Catalog.Bll.Dtos;

/// <summary>Відповідь зі сторінкою даних і метаданими пагінації (метадані в тілі відповіді).</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;
}