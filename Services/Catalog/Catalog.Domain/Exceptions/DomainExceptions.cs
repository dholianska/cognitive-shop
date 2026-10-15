namespace Catalog.Domain.Exceptions;

/// <summary>Порушення бізнес-правила сутності (HTTP 409).</summary>
public class DomainException(string message) : Exception(message);

/// <summary>Запис не знайдено (HTTP 404).</summary>
public class EntityNotFoundException(string message) : Exception(message);

/// <summary>Конфлікт стану, наприклад дубль SKU (HTTP 409).</summary>
public class ConflictException(string message) : Exception(message);

/// <summary>Некоректні вхідні дані (HTTP 400).</summary>
public class ValidationException(string message) : Exception(message);