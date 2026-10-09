using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Exceptions;
using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace EAFCMatchTracker.Application.Services;

public sealed class GoalRegistrationService : IGoalRegistrationService
{
    public const int MaxGoals = 40;
    public const int MaxPerPlayer = 20;
    public const int MaxOpponentNameLength = 100;
    public const int MaxNotesLength = 500;
    public const int DefaultListLimit = 50;
    public const int MaxListLimit = 100;
    public const int ActiveDays = 90;

    private static readonly TimeSpan CurrentWindow = TimeSpan.FromHours(12);
    private static readonly TimeSpan DefaultListWindow = TimeSpan.FromDays(7);

    private readonly IGoalRegistrationRepository _repo;
    private readonly IGoalRegistrationLinker _linker;
    private readonly TimeProvider _time;
    private readonly ILogger<GoalRegistrationService> _logger;

    public GoalRegistrationService(
        IGoalRegistrationRepository repo,
        IGoalRegistrationLinker linker,
        TimeProvider time,
        ILogger<GoalRegistrationService> logger)
    {
        _repo = repo;
        _linker = linker;
        _time = time;
        _logger = logger;
    }

    // ───────────────────────── criação / leitura ─────────────────────────

    public async Task<GoalRegistrationResponseDto> CreateAsync(CreateGoalRegistrationRequest request, CancellationToken ct)
    {
        if (request is null) throw new DomainValidationException("Corpo da requisição ausente.");
        if (request.ClubId <= 0) throw new DomainValidationException("Informe o clube (clubId).");
        if (request.OpponentClubId <= 0) throw new DomainValidationException("Informe o clube adversário (opponentClubId).");
        if (request.OpponentClubId == request.ClubId)
            throw new DomainValidationException("O adversário não pode ser o próprio clube.");

        var name = ValidateName(request.OpponentName);
        var notes = ValidateNotes(request.Notes);
        var lines = request.Goals ?? new List<GoalRegistrationDto>();
        await ValidateLinesAsync(request.ClubId, lines, ct);

        var reg = new GoalRegistrationEntity
        {
            ClubId = request.ClubId,
            OpponentClubId = request.OpponentClubId,
            OpponentName = name,
            Notes = notes,
            CreatedAt = _time.GetUtcNow().UtcDateTime,
            Status = GoalRegistrationStatus.Pending
        };
        var order = 1;
        foreach (var l in lines) reg.Goals.Add(NewGoal(l, order++));

        _repo.Add(reg);
        await _repo.SaveChangesAsync(ct);
        _logger.LogInformation("Registro de gols {Id} criado: clube {ClubId} x {OpponentId} ({Goals} gol(s)).",
            reg.Id, reg.ClubId, reg.OpponentClubId, reg.Goals.Count);

        return await LinkAndRenderAsync(reg, ct);
    }

    public async Task<GoalRegistrationResponseDto?> GetAsync(long id, CancellationToken ct)
    {
        var reg = await _repo.GetWithGoalsAsync(id, ct);
        return reg is null ? null : (await ToDtosAsync(new[] { reg }, ct))[0];
    }

    public async Task<List<GoalRegistrationResponseDto>> ListAsync(long? clubId, string? status, int? limit, CancellationToken ct)
    {
        var take = Math.Clamp(limit ?? DefaultListLimit, 1, MaxListLimit);

        IReadOnlyCollection<GoalRegistrationStatus> statuses;
        DateTime? from = null;
        if (string.IsNullOrWhiteSpace(status))
        {
            statuses = new[] { GoalRegistrationStatus.Pending, GoalRegistrationStatus.NeedsReview, GoalRegistrationStatus.Linked };
            from = _time.GetUtcNow().UtcDateTime - DefaultListWindow;
        }
        else if (Enum.TryParse<GoalRegistrationStatus>(status.Trim(), ignoreCase: true, out var parsed)
                 && Enum.IsDefined(parsed) && !int.TryParse(status.Trim(), out _))
        {
            statuses = new[] { parsed };
        }
        else
        {
            throw new DomainValidationException("Situação inválida. Use Pending, Linked, NeedsReview ou Expired.");
        }

        var list = await _repo.ListAsync(clubId, statuses, from, take, ct);
        return await ToDtosAsync(list, ct);
    }

    public async Task<GoalRegistrationResponseDto?> GetCurrentAsync(long clubId, CancellationToken ct)
    {
        var reg = await _repo.GetCurrentAsync(clubId, _time.GetUtcNow().UtcDateTime - CurrentWindow, ct);
        return reg is null ? null : (await ToDtosAsync(new[] { reg }, ct))[0];
    }

    // ───────────────────────── finalizar / sugestão ─────────────────────────

    public async Task<GoalRegistrationResponseDto> FinishAsync(long id, CancellationToken ct)
    {
        var reg = await _repo.GetWithGoalsAsync(id, ct) ?? throw new KeyNotFoundException($"Registro {id} não encontrado.");
        if (reg.Status == GoalRegistrationStatus.Expired)
            throw new DomainConflictException("Este registro expirou e não pode mais ser finalizado.");
        if (reg.Status == GoalRegistrationStatus.Linked)
            return (await ToDtosAsync(new[] { reg }, ct))[0]; // já concluído: devolve sem alterar

        if (reg.FinishedAt is null)
        {
            reg.FinishedAt = _time.GetUtcNow().UtcDateTime;
            await _repo.SaveChangesAsync(ct);
            _logger.LogInformation("Registro de gols {Id} finalizado.", reg.Id);
        }
        // finalizado + sem partida do adversário informado: o linker já pode sugerir uma partida
        return await LinkAndRenderAsync(reg, ct);
    }

    public async Task<GoalRegistrationResponseDto> ReopenAsync(long id, CancellationToken ct)
    {
        var reg = await _repo.GetWithGoalsAsync(id, ct) ?? throw new KeyNotFoundException($"Registro {id} não encontrado.");
        if (reg.Status != GoalRegistrationStatus.Pending)
            throw new DomainConflictException("Só é possível reabrir um registro pendente.");

        if (reg.FinishedAt is not null)
        {
            reg.FinishedAt = null;
            await _repo.SaveChangesAsync(ct);
            _logger.LogInformation("Registro de gols {Id} reaberto.", reg.Id);
        }
        return (await ToDtosAsync(new[] { reg }, ct))[0];
    }

    public async Task<GoalRegistrationResponseDto> ConfirmSuggestionAsync(long id, CancellationToken ct)
    {
        var reg = await _repo.GetWithGoalsAsync(id, ct) ?? throw new KeyNotFoundException($"Registro {id} não encontrado.");
        if (reg.Status is GoalRegistrationStatus.Expired or GoalRegistrationStatus.Linked)
            throw new DomainConflictException("Este registro não aceita mais uma partida sugerida.");
        if (reg.SuggestedMatchId is not { } matchId || reg.MatchId.HasValue)
            throw new DomainConflictException("Este registro não tem partida sugerida.");

        var match = await _repo.GetMatchAsync(matchId, ct)
                    ?? throw new DomainConflictException("A partida sugerida não existe mais.");
        if (match.GoalRegistrationId is not null && match.GoalRegistrationId != reg.Id)
            throw new DomainConflictException("A partida sugerida já foi vinculada a outro registro.");

        var summary = (await _repo.GetMatchSummariesAsync(new[] { (matchId, reg.ClubId) }, ct)).GetValueOrDefault((matchId, reg.ClubId))
                      ?? throw new DomainConflictException("A partida sugerida não tem dados dos dois clubes.");

        // Mesma validação das edições manuais: se os gols não batem com a partida, nada é alterado
        var failure = await _linker.ValidateGoalsAsync(reg.ClubId, matchId, OrderedLines(reg), ct);
        if (failure is not null) throw new DomainValidationException(failure);

        reg.OpponentClubId = summary.OpponentClubId;
        reg.OpponentName = summary.OpponentName.Length > MaxOpponentNameLength
            ? summary.OpponentName[..MaxOpponentNameLength]
            : summary.OpponentName;
        reg.SuggestedMatchId = null;
        reg.DismissedMatchId = null;
        reg.MatchId = matchId;
        reg.ReviewNote = null;

        await _linker.ReapplyAsync(reg, ct); // cria os MatchGoalLinks e define Linked (ou NeedsReview se houver vínculos manuais)
        await _repo.SaveChangesAsync(ct);
        _logger.LogInformation("Sugestão confirmada: registro {Id} -> partida {MatchId} ({Status}).", reg.Id, matchId, reg.Status);
        return (await ToDtosAsync(new[] { reg }, ct))[0];
    }

    public async Task<GoalRegistrationResponseDto> DismissSuggestionAsync(long id, CancellationToken ct)
    {
        var reg = await _repo.GetWithGoalsAsync(id, ct) ?? throw new KeyNotFoundException($"Registro {id} não encontrado.");
        if (reg.Status is GoalRegistrationStatus.Expired or GoalRegistrationStatus.Linked)
            throw new DomainConflictException("Este registro não tem sugestão a recusar.");

        if (reg.SuggestedMatchId is { } suggested)
        {
            reg.DismissedMatchId = suggested;
            reg.SuggestedMatchId = null;
            if (!reg.MatchId.HasValue)
            {
                reg.Status = GoalRegistrationStatus.Pending;
                reg.ReviewNote = null;
            }
            await _repo.SaveChangesAsync(ct);
            _logger.LogInformation("Sugestão de partida {MatchId} recusada no registro {Id}.", suggested, reg.Id);
        }
        return (await ToDtosAsync(new[] { reg }, ct))[0];
    }

    // ───────────────────────── edição do registro inteiro ─────────────────────────

    public async Task<GoalRegistrationResponseDto> UpdateAsync(long id, UpdateGoalRegistrationRequest request, CancellationToken ct)
    {
        if (request is null) throw new DomainValidationException("Corpo da requisição ausente.");
        var reg = await LoadForEditAsync(id, ct);

        var opponentChanged = request.OpponentClubId.HasValue && request.OpponentClubId.Value != reg.OpponentClubId;
        if (opponentChanged)
        {
            if (reg.Status == GoalRegistrationStatus.Linked)
                throw new DomainConflictException("Registro já vinculado a uma partida; o adversário não pode ser alterado.");
            if (request.OpponentClubId!.Value <= 0 || request.OpponentClubId.Value == reg.ClubId)
                throw new DomainValidationException("O adversário não pode ser o próprio clube.");
            if (string.IsNullOrWhiteSpace(request.OpponentName))
                throw new DomainValidationException("Informe o nome do novo adversário.");
        }

        string? newName = request.OpponentName is null ? null : ValidateName(request.OpponentName);
        string? newNotes = null;
        var notesProvided = request.Notes is not null;
        if (notesProvided) newNotes = ValidateNotes(request.Notes);

        var lines = request.Goals?.ToList();
        if (lines is not null)
        {
            await ValidateLinesAsync(reg.ClubId, lines, ct);
            if (HasMatch(reg) && !opponentChanged)
            {
                var failure = await _linker.ValidateGoalsAsync(reg.ClubId, reg.MatchId!.Value, lines, ct);
                if (failure is not null) throw new DomainValidationException(failure);
            }
        }

        // Troca de adversário: a partida atribuída deixa de valer; o registro volta a procurar candidatas.
        var released = false;
        if (opponentChanged)
        {
            if (reg.MatchId.HasValue)
            {
                var match = await _repo.GetMatchAsync(reg.MatchId.Value, ct);
                if (match is not null && match.GoalRegistrationId == reg.Id) match.GoalRegistrationId = null;
                _repo.RemoveLinks(await _repo.GetOwnLinksAsync(reg.Id, ct));
                released = true;
            }
            reg.OpponentClubId = request.OpponentClubId!.Value;
            reg.MatchId = null;
            reg.Status = GoalRegistrationStatus.Pending;
            reg.LinkedAt = null;
            reg.ReviewNote = null;
        }

        if (newName is not null) reg.OpponentName = newName;
        if (notesProvided) reg.Notes = newNotes;

        if (lines is not null)
        {
            foreach (var g in reg.Goals.ToList())
            {
                reg.Goals.Remove(g);
                _repo.RemoveGoal(g);
            }
            var order = 1;
            foreach (var l in lines) reg.Goals.Add(NewGoal(l, order++));
        }

        if (released) _logger.LogInformation("Registro de gols {Id} trocou de adversário; partida liberada.", reg.Id);
        return await FinishEditAsync(reg, ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        var reg = await _repo.GetWithGoalsAsync(id, ct) ?? throw new KeyNotFoundException($"Registro {id} não encontrado.");
        if (reg.Status == GoalRegistrationStatus.Linked)
            throw new DomainConflictException("Registro já vinculado a uma partida; peça a um administrador.");

        if (reg.MatchId.HasValue)
        {
            var match = await _repo.GetMatchAsync(reg.MatchId.Value, ct);
            if (match is not null && match.GoalRegistrationId == reg.Id) match.GoalRegistrationId = null;
        }

        _repo.RemoveLinks(await _repo.GetOwnLinksAsync(reg.Id, ct));
        _repo.Remove(reg);
        await _repo.SaveChangesAsync(ct);
        _logger.LogInformation("Registro de gols {Id} removido (era {Status}).", id, reg.Status);
    }

    // ───────────────────────── edição gol a gol ─────────────────────────

    public async Task<GoalRegistrationResponseDto> AddGoalAsync(long id, GoalRegistrationDto goal, CancellationToken ct)
    {
        if (goal is null) throw new DomainValidationException("Corpo da requisição ausente.");
        var reg = await LoadForEditAsync(id, ct);

        var current = OrderedLines(reg);
        var resulting = current.Append(goal).ToList();
        await ValidateLinesAsync(reg.ClubId, resulting, ct);
        await ValidateAgainstMatchAsync(reg, resulting, ct);

        var next = reg.Goals.Count == 0 ? 1 : reg.Goals.Max(g => g.Order) + 1;
        reg.Goals.Add(NewGoal(goal, next));
        return await FinishEditAsync(reg, ct);
    }

    public async Task<GoalRegistrationResponseDto> UpdateGoalAsync(long id, long goalId, GoalRegistrationDto goal, CancellationToken ct)
    {
        if (goal is null) throw new DomainValidationException("Corpo da requisição ausente.");
        var reg = await LoadForEditAsync(id, ct);
        var target = reg.Goals.FirstOrDefault(g => g.Id == goalId)
                     ?? throw new KeyNotFoundException($"Gol {goalId} não encontrado neste registro.");

        var resulting = reg.Goals.OrderBy(g => g.Order).ThenBy(g => g.Id)
            .Select(g => g.Id == goalId ? goal : ToLine(g))
            .ToList();
        await ValidateLinesAsync(reg.ClubId, resulting, ct);
        await ValidateAgainstMatchAsync(reg, resulting, ct);

        target.ScorerPlayerEntityId = goal.ScorerPlayerEntityId;
        target.AssistPlayerEntityId = goal.AssistPlayerEntityId;
        target.PreAssistPlayerEntityId = goal.PreAssistPlayerEntityId;
        return await FinishEditAsync(reg, ct);
    }

    public async Task<GoalRegistrationResponseDto> DeleteGoalAsync(long id, long goalId, CancellationToken ct)
    {
        var reg = await LoadForEditAsync(id, ct);
        var target = reg.Goals.FirstOrDefault(g => g.Id == goalId)
                     ?? throw new KeyNotFoundException($"Gol {goalId} não encontrado neste registro.");

        // Remover gols nunca invalida (subconjunto de uma lista válida); NeedsReview é reavaliado em FinishEdit.
        reg.Goals.Remove(target);
        _repo.RemoveGoal(target);
        var order = 1;
        foreach (var g in reg.Goals.OrderBy(g => g.Order).ThenBy(g => g.Id)) g.Order = order++;
        return await FinishEditAsync(reg, ct);
    }

    // ───────────────────────── elenco ─────────────────────────

    public async Task<GoalRosterResponseDto> GetRosterAsync(long clubId, CancellationToken ct)
    {
        if (clubId <= 0) throw new DomainValidationException("Informe o clube (clubId).");

        var rows = await _repo.GetRosterRowsAsync(clubId, ct);
        var ids = rows.Select(r => r.PlayerEntityId).ToList();
        var names = await _repo.GetDisplayNamesAsync(ids, ct);
        var positions = await _repo.GetLastPositionsAsync(clubId, ids, ct);
        var activeSince = _time.GetUtcNow().UtcDateTime.AddDays(-ActiveDays);

        var players = rows
            .Select(r => new GoalRosterPlayerDto
            {
                PlayerEntityId = r.PlayerEntityId,
                Name = names.TryGetValue(r.PlayerEntityId, out var n) ? n : $"#{r.PlayerEntityId}",
                Position = positions.TryGetValue(r.PlayerEntityId, out var pos) ? pos : null,
                MatchesPlayed = r.MatchesPlayed,
                LastPlayedAt = r.LastPlayedAt,
                Active = r.LastPlayedAt >= activeSince
            })
            .OrderByDescending(p => p.Active)
            .ThenByDescending(p => p.LastPlayedAt)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new GoalRosterResponseDto { ClubId = clubId, Players = players };
    }

    // ───────────────────────── helpers ─────────────────────────

    private static bool HasMatch(GoalRegistrationEntity reg) =>
        reg.MatchId.HasValue && reg.Status is GoalRegistrationStatus.Linked or GoalRegistrationStatus.NeedsReview;

    private async Task<GoalRegistrationEntity> LoadForEditAsync(long id, CancellationToken ct)
    {
        var reg = await _repo.GetWithGoalsAsync(id, ct) ?? throw new KeyNotFoundException($"Registro {id} não encontrado.");
        if (reg.Status == GoalRegistrationStatus.Expired)
            throw new DomainConflictException("Este registro expirou e não pode mais ser editado.");

        // Editar os gols/adversário invalida a sugestão (ela depende do nº de gols); o linker sugere de novo se couber.
        if (reg.SuggestedMatchId.HasValue && !reg.MatchId.HasValue)
        {
            reg.SuggestedMatchId = null;
            reg.Status = GoalRegistrationStatus.Pending;
            reg.ReviewNote = null;
        }
        return reg;
    }

    private static List<GoalRegistrationDto> OrderedLines(GoalRegistrationEntity reg) =>
        reg.Goals.OrderBy(g => g.Order).ThenBy(g => g.Id).Select(ToLine).ToList();

    private static GoalRegistrationDto ToLine(GoalRegistrationGoalEntity g) => new()
    {
        ScorerPlayerEntityId = g.ScorerPlayerEntityId,
        AssistPlayerEntityId = g.AssistPlayerEntityId,
        PreAssistPlayerEntityId = g.PreAssistPlayerEntityId
    };

    private static GoalRegistrationGoalEntity NewGoal(GoalRegistrationDto l, int order) => new()
    {
        Order = order,
        ScorerPlayerEntityId = l.ScorerPlayerEntityId,
        AssistPlayerEntityId = l.AssistPlayerEntityId,
        PreAssistPlayerEntityId = l.PreAssistPlayerEntityId
    };

    private async Task ValidateAgainstMatchAsync(GoalRegistrationEntity reg, IReadOnlyList<GoalRegistrationDto> resulting, CancellationToken ct)
    {
        if (!HasMatch(reg)) return;
        var failure = await _linker.ValidateGoalsAsync(reg.ClubId, reg.MatchId!.Value, resulting, ct);
        if (failure is not null) throw new DomainValidationException(failure);
    }

    /// <summary>
    /// Persiste a edição. Com partida atribuída, reaplica a materialização (substitui os MatchGoalLinks) na MESMA
    /// transação (um SaveChanges); sem partida, salva e tenta vincular agora.
    /// </summary>
    private async Task<GoalRegistrationResponseDto> FinishEditAsync(GoalRegistrationEntity reg, CancellationToken ct)
    {
        if (HasMatch(reg))
        {
            await _linker.ReapplyAsync(reg, ct);
            await _repo.SaveChangesAsync(ct);
            return (await ToDtosAsync(new[] { reg }, ct))[0];
        }

        await _repo.SaveChangesAsync(ct);
        return await LinkAndRenderAsync(reg, ct);
    }

    private async Task<GoalRegistrationResponseDto> LinkAndRenderAsync(GoalRegistrationEntity reg, CancellationToken ct)
    {
        try
        {
            await _linker.RunAsync(reg.ClubId, reg.OpponentClubId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // O registro já está salvo (Pending); o próximo ciclo de busca tenta vincular de novo.
            _logger.LogWarning(ex, "Falha ao vincular o registro de gols {Id}; ficará Pending.", reg.Id);
        }

        var fresh = await _repo.GetWithGoalsAsync(reg.Id, ct) ?? reg;
        return (await ToDtosAsync(new[] { fresh }, ct))[0];
    }

    private static string ValidateName(string? raw)
    {
        var name = raw?.Trim();
        if (string.IsNullOrEmpty(name)) throw new DomainValidationException("Informe o nome do adversário.");
        if (name.Length > MaxOpponentNameLength)
            throw new DomainValidationException($"O nome do adversário deve ter no máximo {MaxOpponentNameLength} caracteres.");
        return name;
    }

    private static string? ValidateNotes(string? raw)
    {
        var notes = raw?.Trim();
        if (string.IsNullOrEmpty(notes)) return null;
        if (notes.Length > MaxNotesLength)
            throw new DomainValidationException($"As observações devem ter no máximo {MaxNotesLength} caracteres.");
        return notes;
    }

    /// <summary>Regras de entrada de uma lista de gols (sem consultar a partida real).</summary>
    private async Task ValidateLinesAsync(long clubId, IReadOnlyList<GoalRegistrationDto> lines, CancellationToken ct)
    {
        if (lines.Count > MaxGoals)
            throw new DomainValidationException($"Um registro pode ter no máximo {MaxGoals} gols.");

        foreach (var g in lines)
        {
            if (g is null) throw new DomainValidationException("Gol inválido.");
            if (g.ScorerPlayerEntityId <= 0) throw new DomainValidationException("Informe o artilheiro de cada gol.");

            if (g.AssistPlayerEntityId.HasValue && g.AssistPlayerEntityId.Value == g.ScorerPlayerEntityId)
                throw new DomainValidationException("O assistente deve ser diferente do artilheiro.");

            if (g.PreAssistPlayerEntityId.HasValue)
            {
                if (!g.AssistPlayerEntityId.HasValue)
                    throw new DomainValidationException("A pré-assistência exige uma assistência.");
                if (g.PreAssistPlayerEntityId.Value == g.ScorerPlayerEntityId
                    || g.PreAssistPlayerEntityId.Value == g.AssistPlayerEntityId.Value)
                    throw new DomainValidationException("O pré-assistente deve ser diferente do artilheiro e do assistente.");
            }
        }

        if (lines.GroupBy(g => g.ScorerPlayerEntityId).Any(x => x.Count() > MaxPerPlayer))
            throw new DomainValidationException($"Um jogador não pode ter mais de {MaxPerPlayer} gols em um registro.");
        if (lines.Where(g => g.AssistPlayerEntityId.HasValue).GroupBy(g => g.AssistPlayerEntityId!.Value).Any(x => x.Count() > MaxPerPlayer))
            throw new DomainValidationException($"Um jogador não pode ter mais de {MaxPerPlayer} assistências em um registro.");

        var ids = lines
            .SelectMany(g => new long?[] { g.ScorerPlayerEntityId, g.AssistPlayerEntityId, g.PreAssistPlayerEntityId })
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        if (ids.Count == 0) return;

        var known = (await _repo.GetPlayersAsync(ids, ct)).ToDictionary(p => p.Id);
        foreach (var id in ids)
        {
            if (!known.TryGetValue(id, out var p))
                throw new DomainValidationException($"Jogador {id} não encontrado.");
            if (p.ClubId != clubId)
                throw new DomainValidationException($"O jogador {(string.IsNullOrWhiteSpace(p.Name) ? "#" + id : p.Name)} não pertence ao clube informado.");
        }
    }

    private async Task<List<GoalRegistrationResponseDto>> ToDtosAsync(IReadOnlyList<GoalRegistrationEntity> regs, CancellationToken ct)
    {
        var ids = regs
            .SelectMany(r => r.Goals)
            .SelectMany(g => new long?[] { g.ScorerPlayerEntityId, g.AssistPlayerEntityId, g.PreAssistPlayerEntityId })
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var names = await _repo.GetDisplayNamesAsync(ids, ct);

        string? Name(long? id) => id.HasValue ? (names.TryGetValue(id.Value, out var n) ? n : $"#{id.Value}") : null;

        var suggestedPairs = regs.Where(r => r.SuggestedMatchId.HasValue).Select(r => (r.SuggestedMatchId!.Value, r.ClubId)).ToList();
        var suggested = await _repo.GetMatchSummariesAsync(suggestedPairs, ct);

        return regs.Select(r =>
        {
            var goals = r.Goals.OrderBy(g => g.Order).ThenBy(g => g.Id).ToList();
            GoalRegistrationSuggestedMatchDto? suggestion = null;
            if (r.SuggestedMatchId.HasValue && suggested.TryGetValue((r.SuggestedMatchId.Value, r.ClubId), out var s))
                suggestion = new GoalRegistrationSuggestedMatchDto
                {
                    MatchId = s.MatchId,
                    PlayedAt = s.PlayedAt,
                    OpponentClubId = s.OpponentClubId,
                    OpponentName = s.OpponentName,
                    OurGoals = s.OurGoals,
                    TheirGoals = s.TheirGoals,
                    GoalsMatch = s.OurGoals == goals.Count
                };
            return new GoalRegistrationResponseDto
            {
                Id = r.Id,
                ClubId = r.ClubId,
                OpponentClubId = r.OpponentClubId,
                OpponentName = r.OpponentName,
                Notes = r.Notes,
                CreatedAt = r.CreatedAt,
                StartedAt = r.CreatedAt,
                Status = r.Status.ToString(),
                MatchId = r.MatchId,
                LinkedAt = r.LinkedAt,
                ReviewNote = r.ReviewNote,
                FinishedAt = r.FinishedAt,
                SuggestedMatch = suggestion,
                GoalsCount = goals.Count,
                Goals = goals.Select(g => new GoalRegistrationLineResponseDto
                {
                    Id = g.Id,
                    Order = g.Order,
                    ScorerPlayerEntityId = g.ScorerPlayerEntityId,
                    ScorerName = Name(g.ScorerPlayerEntityId),
                    AssistPlayerEntityId = g.AssistPlayerEntityId,
                    AssistName = Name(g.AssistPlayerEntityId),
                    PreAssistPlayerEntityId = g.PreAssistPlayerEntityId,
                    PreAssistName = Name(g.PreAssistPlayerEntityId)
                }).ToList()
            };
        }).ToList();
    }
}
