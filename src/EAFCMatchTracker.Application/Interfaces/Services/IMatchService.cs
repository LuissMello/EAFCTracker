using EAFCMatchTracker.Application.Dtos;
using DomainMatchType = EAFCMatchTracker.Domain.Entities.MatchType;

namespace EAFCMatchTracker.Application.Interfaces.Services;

public interface IMatchService
{
    Task<PagedResult<MatchDto>> GetAllMatchesAsync(int page, int pageSize, CancellationToken ct);
    Task<MatchDto?> GetMatchByIdAsync(long matchId, CancellationToken ct);
    Task<MatchStatisticsResponseDto?> GetMatchStatisticsByIdAsync(long matchId, CancellationToken ct);
    Task<MatchEventAggregatesResponseDto?> GetMatchEventAggregatesAsync(long matchId, CancellationToken ct);
    Task<MatchPlayerStatsDto?> GetPlayerStatisticsByMatchAndPlayerAsync(long matchId, long playerId, CancellationToken ct);
    Task DeleteMatchAsync(long matchId, CancellationToken ct);
    Task<FullMatchStatisticsDto> GetMatchStatisticsAsync(long clubId, CancellationToken ct);
    Task<FullMatchStatisticsDto> GetMatchStatisticsLimitedAsync(long clubId, int count, int? opponentCount, CancellationToken ct);
    Task<List<FullMatchStatisticsByDayDto>> GetMatchStatisticsByDateRangeGroupedAsync(List<long> ids, DateTime startUtc, DateTime endExclusiveUtc, int? opponentCount, CancellationToken ct);
    Task<List<PlayerStatisticsByDayDto>> GetPlayerMatchStatisticsByDateRangeGroupedAsync(long playerId, List<long> ids, DateTime startUtc, DateTime endExclusiveUtc, CancellationToken ct);
    Task<PagedResult<MatchResultDto>> GetMatchResultsAsync(long clubId, DomainMatchType matchType, int? opponentCount, int page, int pageSize, CancellationToken ct);
    Task<PagedResult<MatchResultDto>> GetMultiClubMatchResultsAsync(List<long> ids, DomainMatchType matchType, int? opponentCount, int page, int pageSize, CancellationToken ct);
    Task DeleteMatchesByClubAsync(long clubId, CancellationToken ct);
    Task<object> GetGroupedLimitedAsync(List<long> ids, int count, int? opponentCount, CancellationToken ct);
    Task<ClubRecordsDto> GetClubRecordsAsync(List<long> ids, CancellationToken ct);
    Task<OpponentsAnalysisDto> GetOpponentsAnalysisAsync(List<long> ids, CancellationToken ct);
}
