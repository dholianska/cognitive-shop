namespace Reviews.Domain.Exceptions;

/// <summary>Порушення бізнес-правила в доменній моделі (HTTP 400).</summary>
public class DomainException(string message) : Exception(message);

/// <summary>Запис не знайдено (HTTP 404).</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>Конфлікт стану, зокрема конфлікт версій документа (HTTP 409).</summary>
public class ConflictException(string message) : Exception(message);