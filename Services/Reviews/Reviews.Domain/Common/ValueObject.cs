namespace Reviews.Domain.Common;

/// <summary>
/// Базовий Value Object: два об'єкти рівні, якщо рівні всі їхні компоненти
/// (порівняння за значенням, а не за посиланням).
/// </summary>
public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
        => obj is ValueObject other
           && GetType() == other.GetType()
           && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());

    public override int GetHashCode()
        => GetEqualityComponents().Aggregate(0, (hash, part) => HashCode.Combine(hash, part));
}