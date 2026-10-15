using Catalog.Domain.Exceptions;

namespace Catalog.Domain;

/// <summary>Спільні перевірки інваріантів для сутностей.</summary>
internal static class Guard
{
    /// <summary>Повертає обрізаний текст; кидає DomainException, якщо він порожній або задовгий.</summary>
    public static string Text(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{field} is required");

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new DomainException($"{field} must not exceed {maxLength} characters");

        return trimmed;
    }

    public static void Positive(decimal value, string field)
    {
        if (value <= 0)
            throw new DomainException($"{field} must be positive");
    }
}