using Reviews.Domain.Common;
using Reviews.Domain.Exceptions;
using Reviews.Domain.ValueObjects;

namespace Reviews.Domain.Entities;

/// <summary>
/// Відгук про товар (документ reviews з Лаб. 01). Корінь агрегата: коментарі змінюються лише через нього.
/// </summary>
public class Review : BaseEntity
{
    public const int ProductNameMaxLength = 200;
    public const int TextMaxLength = 2000;

    private readonly List<Comment> _comments = new();   // вкладений масив

    public long ProductId { get; private set; }                // посилання на Каталог (без FK)
    public string ProductName { get; private set; } = "";     // денормалізовано для читання
    public Author Author { get; private set; } = null!;       // Value Object (вбудований документ)
    public Rating Rating { get; private set; } = null!;       // Value Object
    public string Text { get; private set; } = "";

    public IReadOnlyCollection<Comment> Comments => _comments.AsReadOnly();

    private Review() { }   // для десеріалізації

    public Review(long productId, string productName, Author author, Rating rating, string text)
    {
        if (productId <= 0)
            throw new DomainException("ProductId must be positive");

        ProductName = Guard.Text(productName, nameof(ProductName), ProductNameMaxLength);
        Text = Guard.Text(text, nameof(Text), TextMaxLength);
        Author = author ?? throw new DomainException("Author is required");
        Rating = rating ?? throw new DomainException("Rating is required");
        ProductId = productId;
    }

    public void Update(Rating rating, string text)
    {
        var newText = Guard.Text(text, nameof(Text), TextMaxLength);   // спершу перевірка, потім зміна
        Rating = rating ?? throw new DomainException("Rating is required");
        Text = newText;
        Touch();
    }

    public Comment AddComment(Author author, string text)
    {
        var comment = new Comment(author, text);
        _comments.Add(comment);
        Touch();
        return comment;
    }
}