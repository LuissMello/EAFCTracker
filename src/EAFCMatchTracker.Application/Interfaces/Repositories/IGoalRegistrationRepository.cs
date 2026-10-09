using EAFCMatchTracker.Domain.Entities;

namespace EAFCMatchTracker.Application.Interfaces.Repositories;

public sealed record KnownPlayerRow(long Id, long ClubId, string? Name);

/// <summary>Partida vista do nosso clube: adversário REAL e placar do nosso lado.</summary>
public sealed record MatchSummaryRow(
    long MatchId, DateTime PlayedAt, long OpponentClubId, string OpponentName, int OurGoals, int TheirGoals);

public sealed record RosterRow(long PlayerEntityId, int MatchesPlayed, DateTime LastPlayedAt);

public interface IGoalRegistrationRepository
{
    /// <summary>Registro com os gols (rastreado, para edição). Null se não existir.</summary>
    Task<GoalRegistrationEntity?> GetWithGoalsAsync(long id, CancellationToken ct);

    Task<List<GoalRegistrationEntity>> ListAsync(
        long? clubId, IReadOnlyCollection<GoalRegistrationStatus> statuses, DateTime? createdFromUtc, int limit, CancellationToken ct);

    /// <summary>Resumo (adversário real + placar) das partidas pedidas, do ponto de vista de cada clube; chave = (matchId, clubId).</summary>
    Task<Dictionary<(long MatchId, long ClubId), MatchSummaryRow>> GetMatchSummariesAsync(
        IReadOnlyCollection<(long MatchId, long ClubId)> pairs, CancellationToken ct);

    Task<GoalRegistrationEntity?> GetCurrentAsync(long clubId, DateTime createdFromUtc, CancellationToken ct);

    void Add(GoalRegistrationEntity registration);
    void Remove(GoalRegistrationEntity registration);
    void RemoveGoal(GoalRegistrationGoalEntity goal);

    /// <summary>Partida (rastreada) — usada para soltar o vínculo quando o registro é apagado/trocado de adversário.</summary>
    Task<MatchEntity?> GetMatchAsync(long matchId, CancellationToken ct);

    /// <summary>Links de gols (rastreados) originados deste registro.</summary>
    Task<List<MatchGoalLinkEntity>> GetOwnLinksAsync(long registrationId, CancellationToken ct);

    void RemoveLinks(IEnumerable<MatchGoalLinkEntity> links);

    /// <summary>Jogadores conhecidos (Players) pelos ids, com o clube a que pertencem.</summary>
    Task<List<KnownPlayerRow>> GetPlayersAsync(IReadOnlyCollection<long> playerEntityIds, CancellationToken ct);

    /// <summary>Nome de exibição (último ProName conhecido; senão Playername) por PlayerEntityId.</summary>
    Task<Dictionary<long, string>> GetDisplayNamesAsync(IReadOnlyCollection<long> playerEntityIds, CancellationToken ct);

    /// <summary>Jogadores do clube que já aparecem em partidas: partidas jogadas e última partida.</summary>
    Task<List<RosterRow>> GetRosterRowsAsync(long clubId, CancellationToken ct);

    /// <summary>Última posição (Pos) conhecida do jogador pelo clube.</summary>
    Task<Dictionary<long, string>> GetLastPositionsAsync(long clubId, IReadOnlyCollection<long> playerEntityIds, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
