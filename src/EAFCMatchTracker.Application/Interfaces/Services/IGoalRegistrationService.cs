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

    /// <summary>Registro mais recente do clube em Pending/NeedsReview criado nas últimas 12h; null se não houver.</summary>
    Task<GoalRegistrationResponseDto?> GetCurrentAsync(long clubId, CancellationToken ct);

    Task<GoalRegistrationResponseDto> UpdateAsync(long id, UpdateGoalRegistrationRequest request, CancellationToken ct);
    Task DeleteAsync(long id, CancellationToken ct);

    Task<GoalRegistrationResponseDto> AddGoalAsync(long id, GoalRegistrationDto goal, CancellationToken ct);
    Task<GoalRegistrationResponseDto> UpdateGoalAsync(long id, long goalId, GoalRegistrationDto goal, CancellationToken ct);
    Task<GoalRegistrationResponseDto> DeleteGoalAsync(long id, long goalId, CancellationToken ct);

    Task<GoalRosterResponseDto> GetRosterAsync(long clubId, CancellationToken ct);
}
