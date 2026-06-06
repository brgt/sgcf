namespace Sgcf.Application.Cotacoes.Exceptions;

/// <summary>
/// Conflito de estado em uma operação de cotação (transição/edição inválida para o status atual).
/// Mapeada para HTTP 409 ProblemDetails (type conflito-de-estado). SPEC S40 §5.
/// Especialização de <see cref="InvalidOperationException"/> para mapeamento tipado central.
/// </summary>
public sealed class ConflitoDeEstadoException(string message) : InvalidOperationException(message);
