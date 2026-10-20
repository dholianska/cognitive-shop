using Reviews.Domain.Common;

namespace Reviews.Domain.ValueObjects;

/// <summary>Автор відгуку чи коментаря (вбудований документ author).</summary>
public sealed class Author : ValueObject
{
    public const int UserIdMaxLength = 50;
    public const int DisplayNameMaxLength = 100;

    public string UserId { get; }
    public string DisplayName { get; }

    public Author(string userId, string displayName)
    {
        UserId = Guard.Text(userId, nameof(UserId), UserIdMaxLength);
        DisplayName = Guard.Text(displayName, nameof(DisplayName), DisplayNameMaxLength);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return UserId;
        yield return DisplayName;
    }
}