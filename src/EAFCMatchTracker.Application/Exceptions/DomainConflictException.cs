namespace EAFCMatchTracker.Application.Exceptions;

/// <summary>
/// Operação inválida para o estado atual do recurso (ex.: editar um registro expirado). Mensagem pt-BR segura para
/// exibição; deve ser devolvida em um HTTP 409.
/// </summary>
public sealed class DomainConflictException : Exception
{
    public DomainConflictException(string message) : base(message) { }
}
