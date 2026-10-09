using EAFCMatchTracker.Application.Dtos;

namespace EAFCMatchTracker.Application.Interfaces.Services;

/// <summary>
/// Registro de gols ao vivo / antecipado. Erros esperados: <see cref="Exceptions.DomainValidationException"/> (400),
/// <see cref="Exceptions.DomainConflictException"/> (409) e <see cref="KeyNotFoundException"/> (404).
/// </summary>
public interface IGoalRegistrationService
{
    Task<GoalRegistrationResponseDto> CreateAsync(CreateGoalRegistrationRequest request, CancellationToken ct);
    Task<GoalRegistrationResponseDto?> GetAsync(long id, CancellationToken ct);
    Task<List<GoalRegistrationResponseDto>> ListAsync(long? clubId, string? status, int? limit, CancellationToken ct);

    /// <summary>Registro EM ANDAMENTO do clube: Pending, não finalizado (FinishedAt nulo), criado nas últimas 12h; null se não houver.</summary>
    Task<GoalRegistrationResponseDto?> GetCurrentAsync(long clubId, CancellationToken ct);

    Task<GoalRegistrationResponseDto> UpdateAsync(long id, UpdateGoalRegistrationRequest request, CancellationToken ct);
    Task DeleteAsync(long id, CancellationToken ct);

    Task<GoalRegistrationResponseDto> AddGoalAsync(long id, GoalRegistrationDto goal, CancellationToken ct);
    Task<GoalRegistrationResponseDto> UpdateGoalAsync(long id, long goalId, GoalRegistrationDto goal, CancellationToken ct);
    Task<GoalRegistrationResponseDto> DeleteGoalAsync(long id, long goalId, CancellationToken ct);

    /// <summary>
    /// Finaliza (FinishedAt = agora) um registro Pending/NeedsReview; idempotente. Linked devolve o registro sem alterar;
    /// Expired lança conflito (409).
    /// </summary>
    Task<GoalRegistrationResponseDto> FinishAsync(long id, CancellationToken ct);

    /// <summary>Desfaz o "finalizar" (FinishedAt = nulo). Só Pending; os demais estados lançam conflito (409). Idempotente.</summary>
    Task<GoalRegistrationResponseDto> ReopenAsync(long id, CancellationToken ct);

    /// <summary>
    /// Aceita a partida sugerida: adota o adversário real dela, valida os gols (mesma regra das edições manuais; falha = 400 com a
    /// mensagem amigável) e vincula. Sem sugestão: conflito (409).
    /// </summary>
    Task<GoalRegistrationResponseDto> ConfirmSuggestionAsync(long id, CancellationToken ct);

    /// <summary>Recusa a sugestão: limpa SuggestedMatchId, volta a Pending e não sugere a mesma partida de novo. Idempotente.</summary>
    Task<GoalRegistrationResponseDto> DismissSuggestionAsync(long id, CancellationToken ct);

    Task<GoalRosterResponseDto> GetRosterAsync(long clubId, CancellationToken ct);
}
