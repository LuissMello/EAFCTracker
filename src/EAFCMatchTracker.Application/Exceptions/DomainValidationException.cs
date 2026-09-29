namespace EAFCMatchTracker.Application.Exceptions;

/// <summary>
/// Erro de validação de regra de negócio esperado (entrada do cliente inválida). A mensagem é escrita pelo
/// próprio serviço, é segura para exibição e deve ser devolvida em um HTTP 400. Não use para falhas internas.
/// </summary>
public sealed class DomainValidationException : Exception
{
    public DomainValidationException(string message) : base(message) { }
}
