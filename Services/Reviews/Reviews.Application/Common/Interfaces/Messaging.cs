using MediatR;

namespace Reviews.Application.Common.Interfaces;

/// <summary>Команда без результату (змінює стан).</summary>
public interface ICommand : IRequest { }

/// <summary>Команда з результатом.</summary>
public interface ICommand<TResponse> : IRequest<TResponse> { }

/// <summary>Запит (лише читає).</summary>
public interface IQuery<TResponse> : IRequest<TResponse> { }