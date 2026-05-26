using EAFCMatchTracker.Domain.Entities;

namespace EAFCMatchTracker.Application.Dtos;

/// <summary>
/// Lógica centralizada de parsing dos campos match_event_aggregate_0~3.
/// Usada por MatchesController e MaintenanceController.
/// </summary>
public static class MatchAggregateParser
{
    /// <summary>
    /// Lê uma string no formato "ID:valor,ID:valor,..." e acumula os valores no dicionário.
    /// </summary>
    public static void MergeAggregate(string? raw, Dictionary<string, int> events)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;

        foreach (var pair in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = pair.IndexOf(':');
            if (idx < 1) continue;

            var key = pair[..idx].Trim();
            if (!int.TryParse(pair[(idx + 1)..].Trim(), out var value)) continue;

            events.TryGetValue(key, out var existing);
            events[key] = existing + value;
        }
    }

    /// <summary>
    /// Constrói a lista de PlayerEventAggregatesDto para um conjunto de jogadores.
    /// Ignora jogadores sem nenhum dado de aggregate.
    /// </summary>
    public static List<PlayerEventAggregatesDto> BuildPlayerAggregates(
        IEnumerable<MatchPlayerEntity> players)
    {
        var result = new List<PlayerEventAggregatesDto>();

        foreach (var player in players)
        {
            var events = new Dictionary<string, int>();
            MergeAggregate(player.MatchEventAggregate0, events);
            MergeAggregate(player.MatchEventAggregate1, events);
            MergeAggregate(player.MatchEventAggregate2, events);
            MergeAggregate(player.MatchEventAggregate3, events);

            if (events.Count == 0) continue;

            result.Add(new PlayerEventAggregatesDto
            {
                PlayerId  = player.PlayerEntityId,
                PlayerName = player.Player?.Playername ?? player.ProName ?? player.PlayerEntityId.ToString(),
                Events    = events
            });
        }

        return result;
    }

    /// <summary>
    /// Constrói a lista de ClubEventAggregatesDto para uma partida,
    /// incluindo o placar e os aggregates individuais de cada jogador.
    /// </summary>
    public static List<ClubEventAggregatesDto> BuildClubAggregates(
        IEnumerable<MatchClubEntity> clubs,
        IEnumerable<MatchPlayerEntity> allPlayers)
    {
        var playersByClub = allPlayers
            .GroupBy(p => p.ClubId)
            .ToDictionary(g => g.Key, g => (IEnumerable<MatchPlayerEntity>)g);

        return clubs.Select(club => new ClubEventAggregatesDto
        {
            ClubId       = club.ClubId,
            ClubName     = club.Details?.Name ?? club.ClubId.ToString(),
            CrestAssetId = club.Details?.CrestAssetId,
            Goals        = club.Goals,
            GoalsAgainst = club.GoalsAgainst,
            Players      = BuildPlayerAggregates(
                playersByClub.GetValueOrDefault(club.ClubId, Enumerable.Empty<MatchPlayerEntity>()))
        }).ToList();
    }
}
