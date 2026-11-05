namespace Reviews.Application.Common.Models;

/// <summary>Плоске представлення відгуку для читання. Version потрібна для ETag / If-Match.</summary>
public class ReviewDto
{
    public string Id { get; set; } = "";
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public int Rating { get; set; }
    public string Text { get; set; } = "";
    public int CommentsCount { get; set; }
    public int Version { get; set; }
}