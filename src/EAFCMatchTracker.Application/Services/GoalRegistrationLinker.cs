using System.Globalization;
using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EAFCMatchTracker.Application.Services;

public sealed class GoalRegistrationLinker : IGoalRegistrationLinker
{
    public const string AmbiguousNote =
        "Havia mais de uma partida candidata contra este adversário; confira se o vínculo está correto.";
    public const string ManualLinksNote =
        "Já existem vínculos manuais nesta partida; o registro não foi aplicado para não sobrescrevê-los.";

    private const string UniqueViolationSqlState = "23505";

    // Serializa execuções dentro do processo (ciclo de busca x criação/edição pela API). Entre processos, valem os
    // índices únicos filtrados (Matches.GoalRegistrationId e GoalRegistrations.MatchId).
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly EAFCContext _db;
    private readonly TimeProvider _time;
    private readonly ILogger<GoalRegistrationLinker> _logger;
    private readonly TimeSpan _slack;

    /// <summary>Tolerância padrão (min) para quem clica em "Iniciar" um pouco depois do início do jogo.</summary>
    public const int DefaultSlackMinutes = 15;

    public GoalRegistrationLinker(
        EAFCContext db, TimeProvider time, ILogger<GoalRegistrationLinker> logger, IConfiguration? config = null)
    {
        _db = db;
        _time = time;
        _logger = logger;
        var slack = int.TryParse(config?["GoalRegistration:LinkSlackMinutes"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) && m >= 0
            ? m
            : DefaultSlackMinutes;
        _slack = TimeSpan.FromMinutes(slack);
    }

    // Janela ASSIMÉTRICA (uso ao vivo: o registro nasce no início do jogo): uma partida é candidata se
    //   createdAt - slack <= Match.Timestamp <= createdAt + window   (window = goal_link_window_minutes).
    // Partidas anteriores ao registro (fora do slack) são de outro jogo.
    private bool InWindow(DateTime matchTimestamp, DateTime createdAt, TimeSpan window) =>
        matchTimestamp >= createdAt - _slack && matchTimestamp <= createdAt + window;

    private sealed record Outcome(GoalRegistrationStatus Status, string? Note);

    public async Task<GoalLinkRunResult> RunAsync(long? clubId = null, long? opponentClubId = null, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var window = TimeSpan.FromMinutes(await GetSettingAsync(AppSettingEntity.Keys.GoalLinkWindowMinutes, ct));
            var expireDays = await GetSettingAsync(AppSettingEntity.Keys.GoalRegistrationExpireDays, ct);
            var now = _time.GetUtcNow().UtcDateTime;

            int linked = 0, review = 0, expired = 0;

            // 1) Higiene
            var orphans = await _db.GoalRegistrations
                .Where(r => (r.Status == GoalRegistrationStatus.Linked || r.Status == GoalRegistrationStatus.NeedsReview)
                            && r.MatchId == null)
                .ToListAsync(ct);
            foreach (var r in orphans)
            {
                _logger.LogInformation("Registro de gols {Id} perdeu a partida (apagada); voltando a Pending.", r.Id);
                r.Status = GoalRegistrationStatus.Pending;
                r.LinkedAt = null;
                r.ReviewNote = null;
            }
            if (orphans.Count > 0) await _db.SaveChangesAsync(ct);

            var cutoff = now.AddDays(-expireDays);
            var stale = await _db.GoalRegistrations
                .Where(r => r.Status == GoalRegistrationStatus.Pending && r.CreatedAt < cutoff)
                .ToListAsync(ct);
            foreach (var r in stale)
            {
                r.Status = GoalRegistrationStatus.Expired;
                expired++;
                _logger.LogInformation("Registro de gols {Id} expirou (criado em {CreatedAt:o}).", r.Id, r.CreatedAt);
            }
            if (stale.Count > 0) await _db.SaveChangesAsync(ct);

            // 2) Grupos Pending
            var keysQuery = _db.GoalRegistrations.AsNoTracking().Where(r => r.Status == GoalRegistrationStatus.Pending);
            if (clubId.HasValue) keysQuery = keysQuery.Where(r => r.ClubId == clubId.Value);
            if (opponentClubId.HasValue) keysQuery = keysQuery.Where(r => r.OpponentClubId == opponentClubId.Value);
            var keys = await keysQuery
                .Select(r => new { r.ClubId, r.OpponentClubId })
                .Distinct()
                .ToListAsync(ct);

            foreach (var key in keys)
            {
                try
                {
                    var (l, n) = await ProcessGroupAsync(key.ClubId, key.OpponentClubId, window, now, ct);
                    linked += l;
                    review += n;
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    _logger.LogInformation(ex,
                        "Vínculo de registros de gols ignorado (violação de unicidade; outro processo vinculou antes). Clube={ClubId} Adversário={OpponentId}",
                        key.ClubId, key.OpponentClubId);
                    _db.ChangeTracker.Clear();
                }
            }

            if (linked + review + expired > 0)
                _logger.LogInformation(
                    "Linker de gols: {Linked} vinculado(s), {Review} para revisão, {Expired} expirado(s).", linked, review, expired);

            return new GoalLinkRunResult(linked, review, expired);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<GoalRegistrationStatus> ReapplyAsync(GoalRegistrationEntity reg, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var match = reg.MatchId.HasValue
                ? await _db.Matches.FirstOrDefaultAsync(m => m.MatchId == reg.MatchId.Value, ct)
                : null;

            if (match is null)
            {
                reg.MatchId = null;
                reg.Status = GoalRegistrationStatus.Pending;
                reg.LinkedAt = null;
                reg.ReviewNote = null;
                return reg.Status;
            }

            var previousLinkedAt = reg.Status == GoalRegistrationStatus.Linked ? reg.LinkedAt : null;
            var outcome = await MaterializeAsync(reg, match, ct);
            Apply(reg, outcome, _time.GetUtcNow().UtcDateTime, previousLinkedAt);
            _logger.LogInformation("Registro de gols {Id} reaplicado à partida {MatchId}: {Status}.", reg.Id, match.MatchId, reg.Status);
            return reg.Status;
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<string?> ValidateGoalsAsync(long clubId, long matchId, IReadOnlyList<GoalRegistrationDto> goals, CancellationToken ct = default)
    {
        var ctx = await LoadMatchContextAsync(clubId, matchId, ct);
        return await FindFailureAsync(goals, ctx, ct);
    }

    private async Task<(int Linked, int Review)> ProcessGroupAsync(
        long clubId, long opponentId, TimeSpan window, DateTime now, CancellationToken ct)
    {
        var regs = await _db.GoalRegistrations
            .Include(r => r.Goals)
            .Where(r => r.Status == GoalRegistrationStatus.Pending && r.ClubId == clubId && r.OpponentClubId == opponentId)
            .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
            .ToListAsync(ct);
        if (regs.Count == 0) return (0, 0);

        var from = regs.Min(r => r.CreatedAt) - _slack;
        var to = regs.Max(r => r.CreatedAt) + window;

        var candidates = await _db.Matches
            .Where(m => m.GoalRegistrationId == null
                        && m.Timestamp >= from && m.Timestamp <= to
                        && m.Clubs.Any(c => c.ClubId == clubId)
                        && m.Clubs.Any(c => c.ClubId == opponentId)
                        && !_db.GoalRegistrations.Any(r => r.MatchId == m.MatchId))
            .OrderBy(m => m.Timestamp).ThenBy(m => m.MatchId)
            .ToListAsync(ct);
        if (candidates.Count == 0) return (0, 0);

        // Pareamento em ordem (jogos consecutivos contra o mesmo adversário): cada candidata é usada uma vez e
        // cada registro só aceita candidatas dentro da SUA janela.
        var used = new HashSet<long>();
        var pairs = new List<(GoalRegistrationEntity Reg, MatchEntity Match)>();
        foreach (var reg in regs)
        {
            var match = candidates.FirstOrDefault(c => !used.Contains(c.MatchId)
                && InWindow(c.Timestamp, reg.CreatedAt, window));
            if (match is null) continue;
            used.Add(match.MatchId);
            pairs.Add((reg, match));
        }

        // Mais candidatas elegíveis (na janela combinada do grupo) do que registros Pending: não dá para ter certeza
        var ambiguous = candidates.Count > regs.Count;
        int linked = 0, review = 0;

        foreach (var (reg, match) in pairs)
        {
            Outcome outcome;
            if (ambiguous)
            {
                reg.MatchId = match.MatchId;
                match.GoalRegistrationId = reg.Id;
                outcome = new Outcome(GoalRegistrationStatus.NeedsReview, AmbiguousNote);
            }
            else
            {
                outcome = await MaterializeAsync(reg, match, ct);
            }

            Apply(reg, outcome, now);
            if (reg.Status == GoalRegistrationStatus.Linked) linked++;
            else review++;

            _logger.LogInformation(
                "Registro de gols {Id} -> partida {MatchId}: {Status}{Note}",
                reg.Id, match.MatchId, reg.Status, reg.ReviewNote is null ? "" : $" ({reg.ReviewNote})");
        }

        await _db.SaveChangesAsync(ct);
        return (linked, review);
    }

    private static void Apply(GoalRegistrationEntity reg, Outcome outcome, DateTime now, DateTime? keepLinkedAt = null)
    {
        reg.Status = outcome.Status;
        reg.ReviewNote = outcome.Note;
        reg.LinkedAt = outcome.Status == GoalRegistrationStatus.Linked ? (keepLinkedAt ?? now) : null;
    }

    private sealed record MatchContext(
        List<MatchPlayerEntity> Players, Dictionary<long, MatchPlayerEntity> ByPlayer, int? RealClubGoals);

    private async Task<MatchContext> LoadMatchContextAsync(long clubId, long matchId, CancellationToken ct)
    {
        var players = await _db.MatchPlayers
            .Include(mp => mp.Player)
            .Where(mp => mp.MatchId == matchId && mp.ClubId == clubId)
            .ToListAsync(ct);
        var byPlayer = players.GroupBy(p => p.PlayerEntityId).ToDictionary(g => g.Key, g => g.First());
        var real = await _db.MatchClubs
            .Where(c => c.MatchId == matchId && c.ClubId == clubId)
            .Select(c => (int?)c.Goals)
            .FirstOrDefaultAsync(ct);
        return new MatchContext(players, byPlayer, real);
    }

    /// <summary>
    /// Regras de GoalAnalysisService.RegisterGoalsAsync aplicadas às linhas do registro, mais o total do clube:
    /// primeiro problema (pt-BR) ou null.
    /// </summary>
    private async Task<string?> FindFailureAsync(IReadOnlyList<GoalRegistrationDto> goals, MatchContext ctx, CancellationToken ct)
    {
        var byPlayer = ctx.ByPlayer;

        string NameOf(long id) =>
            byPlayer.TryGetValue(id, out var mp)
                ? (!string.IsNullOrWhiteSpace(mp.ProName) ? mp.ProName! : mp.Player?.Playername ?? $"#{id}")
                : $"#{id}";

        foreach (var g in goals)
        {
            if (!byPlayer.ContainsKey(g.ScorerPlayerEntityId))
                return $"O artilheiro {await DisplayNameAsync(g.ScorerPlayerEntityId, ct)} não aparece entre os jogadores do seu clube nesta partida.";

            if (g.AssistPlayerEntityId is { } a)
            {
                if (!byPlayer.ContainsKey(a))
                    return $"O assistente {await DisplayNameAsync(a, ct)} não aparece entre os jogadores do seu clube nesta partida.";
                if (a == g.ScorerPlayerEntityId)
                    return $"{NameOf(a)} não pode assistir o próprio gol.";
            }

            if (g.PreAssistPlayerEntityId is { } p)
            {
                if (!byPlayer.ContainsKey(p))
                    return $"O pré-assistente {await DisplayNameAsync(p, ct)} não aparece entre os jogadores do seu clube nesta partida.";
                if (p == g.ScorerPlayerEntityId || p == g.AssistPlayerEntityId)
                    return $"O pré-assistente {NameOf(p)} deve ser diferente do artilheiro e do assistente.";
            }
        }

        foreach (var kv in goals.GroupBy(g => g.ScorerPlayerEntityId))
        {
            var real = byPlayer[kv.Key].Goals;
            if (kv.Count() > real)
                return $"{NameOf(kv.Key)} foi registrado com {kv.Count()} gol(s), mas a EA contabilizou {real}.";
        }

        foreach (var kv in goals.Where(g => g.AssistPlayerEntityId.HasValue).GroupBy(g => g.AssistPlayerEntityId!.Value))
        {
            var real = byPlayer[kv.Key].Assists;
            if (kv.Count() > real)
                return $"{NameOf(kv.Key)} foi registrado com {kv.Count()} assistência(s), mas a EA contabilizou {real}.";
        }

        if (ctx.RealClubGoals is null)
            return "A partida não possui dados do seu clube.";

        if (goals.Count > ctx.RealClubGoals.Value)
            return $"Foram registrados {goals.Count} gols, mas a partida teve apenas {ctx.RealClubGoals.Value}.";

        return null;
    }

    /// <summary>Nota suave quando foram registrados menos gols do que a partida teve (null se igual).</summary>
    public static string? SoftNote(int registered, int real) =>
        registered < real
            ? string.Format(CultureInfo.InvariantCulture, "Foram registrados {0} gols; a partida teve {1}.", registered, real)
            : null;

    /// <summary>
    /// Valida o registro contra a partida e, se tudo certo, cria os MatchGoalLinks (substituindo os do próprio
    /// registro). Sempre deixa reg.MatchId / match.GoalRegistrationId atribuídos. Não salva.
    /// </summary>
    private async Task<Outcome> MaterializeAsync(GoalRegistrationEntity reg, MatchEntity match, CancellationToken ct)
    {
        reg.MatchId = match.MatchId;
        match.GoalRegistrationId = reg.Id;

        var existingLinks = await _db.MatchGoalLinks.Where(l => l.MatchId == match.MatchId).ToListAsync(ct);
        var ownLinks = existingLinks.Where(l => l.GoalRegistrationId == reg.Id).ToList();

        var ctx = await LoadMatchContextAsync(reg.ClubId, match.MatchId, ct);

        // Nunca sobrescreve vínculos manuais
        var foreign = existingLinks.Any(l => l.GoalRegistrationId != reg.Id
                                             && (l.ClubId == reg.ClubId || ctx.ByPlayer.ContainsKey(l.ScorerPlayerEntityId)));
        if (foreign)
            return new Outcome(GoalRegistrationStatus.NeedsReview, ManualLinksNote);

        var goals = reg.Goals.OrderBy(g => g.Order).ThenBy(g => g.Id).ToList();
        var lines = goals.Select(g => new GoalRegistrationDto
        {
            ScorerPlayerEntityId = g.ScorerPlayerEntityId,
            AssistPlayerEntityId = g.AssistPlayerEntityId,
            PreAssistPlayerEntityId = g.PreAssistPlayerEntityId
        }).ToList();

        var failure = await FindFailureAsync(lines, ctx, ct);

        // Remove links anteriores deste mesmo registro (reaplicação idempotente)
        if (ownLinks.Count > 0) _db.MatchGoalLinks.RemoveRange(ownLinks);

        if (failure is not null)
        {
            RecomputePreAssists(ctx.Players, Enumerable.Empty<MatchGoalLinkEntity>());
            return new Outcome(GoalRegistrationStatus.NeedsReview, failure);
        }

        var newLinks = goals.Select(g => new MatchGoalLinkEntity
        {
            MatchId = match.MatchId,
            ClubId = reg.ClubId,
            ScorerPlayerEntityId = g.ScorerPlayerEntityId,
            AssistPlayerEntityId = g.AssistPlayerEntityId,
            PreAssistPlayerEntityId = g.PreAssistPlayerEntityId,
            GoalRegistrationId = reg.Id
        }).ToList();
        _db.MatchGoalLinks.AddRange(newLinks);
        RecomputePreAssists(ctx.Players, newLinks);

        return new Outcome(GoalRegistrationStatus.Linked, SoftNote(goals.Count, ctx.RealClubGoals!.Value));
    }

    // PreAssists = quantidade de links finais em que o jogador é o pré-assistente (como em RegisterGoalsAsync)
    private static void RecomputePreAssists(IEnumerable<MatchPlayerEntity> players, IEnumerable<MatchGoalLinkEntity> links)
    {
        var counts = links.Where(l => l.PreAssistPlayerEntityId.HasValue)
            .GroupBy(l => l.PreAssistPlayerEntityId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (var p in players)
            p.PreAssists = (short)(counts.TryGetValue(p.PlayerEntityId, out var n) ? n : 0);
    }

    private async Task<string> DisplayNameAsync(long playerEntityId, CancellationToken ct)
    {
        var name = await _db.Players.AsNoTracking().Where(p => p.Id == playerEntityId).Select(p => p.Playername).FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(name) ? $"#{playerEntityId}" : name;
    }

    private async Task<int> GetSettingAsync(string key, CancellationToken ct)
    {
        var def = AppSettingEntity.Definitions.Find(key)!;
        var raw = await _db.AppSettings.AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync(ct);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? Math.Clamp(v, def.Min, def.Max)
            : def.Default;
    }

    private static bool IsUniqueViolation(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
            if (e is PostgresException { SqlState: UniqueViolationSqlState }) return true;
        return false;
    }
}
