using EAFCMatchTracker.Application.Dtos;

namespace EAFCMatchTracker.Application.Interfaces.Services;

public interface IWrappedService
{
    Task<WrappedDto> GetWrappedAsync(long clubId, int? gameVersion, CancellationToken ct);
}
