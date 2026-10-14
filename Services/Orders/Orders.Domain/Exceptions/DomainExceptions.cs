namespace Orders.Domain.Exceptions;

/// <summary>Запис не знайдено (HTTP 404).</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>Порушення бізнес-правила, наприклад, недопустимий перехід статусу (HTTP 409).</summary>
public class BusinessConflictException(string message) : Exception(message);

/// <summary>Некоректні вхідні дані (HTTP 400).</summary>
public class ValidationException(string message) : Exception(message);