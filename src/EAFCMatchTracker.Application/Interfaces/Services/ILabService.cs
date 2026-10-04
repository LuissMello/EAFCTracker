using EAFCMatchTracker.Application.Dtos;

namespace EAFCMatchTracker.Application.Interfaces.Services;

public interface ILabService
{
    Task<LabPlayerImpactDto> GetPlayerImpactAsync(
        long clubId, DateOnly? from, DateOnly? to, int? gameVersion, int minMatches, CancellationToken ct);

    Task<LabContextDto> GetContextAsync(
        long clubId, DateOnly? from, DateOnly? to, int? gameVersion, CancellationToken ct);

    Task<LabDuosDto> GetDuosAsync(
        long clubId, DateOnly? from, DateOnly? to, int? gameVersion, int minMatches, CancellationToken ct);
}
