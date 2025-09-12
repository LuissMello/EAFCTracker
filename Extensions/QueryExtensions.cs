using EAFCMatchTracker.Dtos;
using Microsoft.EntityFrameworkCore;

namespace EAFCMatchTracker.Extensions;

public static class QueryExtensions
{
    public static IQueryable<MatchDto> ProjectMatches(this IQueryable<MatchEntity> query) =>
        query.Select(DtoProjections.Match);

    public static Task<List<MatchDto>> ToMatchDtoListAsync(this IQueryable<MatchEntity> query, CancellationToken ct) =>
        query.ProjectMatches().ToListAsync(ct);

    public static Task<MatchDto?> FirstMatchDtoOrDefaultAsync(this IQueryable<MatchEntity> query, CancellationToken ct) =>
        query.ProjectMatches().FirstOrDefaultAsync(ct);

    public static IQueryable<MatchPlayerStatsDto> ProjectPlayerStats(this IQueryable<MatchPlayerEntity> query) =>
        query.Select(DtoProjections.MatchPlayerStatsRow);
}