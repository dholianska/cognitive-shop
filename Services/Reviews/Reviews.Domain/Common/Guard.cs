using Reviews.Domain.Exceptions;

namespace Reviews.Domain.Common;

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
}