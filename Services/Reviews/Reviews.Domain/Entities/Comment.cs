using Reviews.Domain.Common;
using Reviews.Domain.ValueObjects;

namespace Reviews.Domain.Entities;

/// <summary>Коментар до відгуку: вбудована сутність зі своїм ObjectId (CreatedAt/UpdatedAt із BaseEntity).</summary>
public class Comment : BaseEntity
{
    public const int TextMaxLength = 1000;

    public Author Author { get; private set; } = null!;
    public string Text { get; private set; } = "";

    private Comment() { }   // для десеріалізації

    public Comment(Author author, string text)
    {
        Author = author ?? throw new Exceptions.DomainException("Author is required");
        Text = Guard.Text(text, nameof(Text), TextMaxLength);
    }

    public void Edit(string text)
    {
        Text = Guard.Text(text, nameof(Text), TextMaxLength);
        Touch();
    }
}