using System.Collections.Concurrent;
using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EAFCMatchTracker.Application.Services.Analytics;

/// <summary>Grupos de posição válidos para o catálogo (os mesmos de <see cref="CardScoring"/>).</summary>
public static class ArchetypeGroups
{
    public static readonly string[] All =
        { CardScoring.GroupAttack, CardScoring.GroupMidfield, CardScoring.GroupDefense, CardScoring.GroupKeeper };

    /// <summary>"ataque"/" ATAQUE " -> "ATAQUE"; valor desconhecido -> nulo.</summary>
    public static string? Normalize(string? raw)
    {
        var v = raw?.Trim().ToUpperInvariant();
        return v is not null && Array.IndexOf(All, v) >= 0 ? v : null;
    }

    /// <summary>
    /// Grupo mais frequente entre as posições jogadas com o arquétipo (via <see cref="CardScoring.PositionGroup"/>);
    /// posições vazias são ignoradas. Empate: ordem ATAQUE, MEIO, DEFESA, GOLEIRO. Sem posição alguma: nulo.
    /// </summary>
    public static string? Infer(IEnumerable<(string Pos, int Count)> positions)
    {
        var counts = new Dictionary<string, int>();
        foreach (var (pos, n) in positions)
        {
            if (string.IsNullOrWhiteSpace(pos) || n <= 0) continue;
            var g = CardScoring.PositionGroup(pos);
            counts[g] = counts.GetValueOrDefault(g) + n;
        }
        return counts.Count == 0
            ? null
            : counts.OrderByDescending(kv => kv.Value).ThenBy(kv => Array.IndexOf(All, kv.Key)).First().Key;
    }
}

/// <summary>
/// Mapa conhecido id -> (nome, grupo) dos 13 arquétipos do jogo. É o padrão EM CÓDIGO (vale quando a tabela
/// PlayerArchetypes não existe/está vazia para o id) e também o conteúdo semeado pela migration AddPlayerArchetypes.
/// Linha existente no banco (editada no Admin) sempre vence. Ids fora de 1..13 (ex.: 30) ficam "Arquétipo #id".
/// </summary>
public static class ArchetypeDefaults
{
    public static readonly IReadOnlyDictionary<int, (string Name, string Group)> Known = new Dictionary<int, (string, string)>
    {
        [1] = ("Shot Stopper", CardScoring.GroupKeeper),
        [2] = ("Sweeper Keeper", CardScoring.GroupKeeper),
        [3] = ("Progressor", CardScoring.GroupDefense),
        [4] = ("Boss", CardScoring.GroupDefense),
        [5] = ("Disruptor", CardScoring.GroupDefense),
        [6] = ("Marauder", CardScoring.GroupDefense),
        [7] = ("Recycler", CardScoring.GroupMidfield),
        [8] = ("Maestro", CardScoring.GroupMidfield),
        [9] = ("Creator", CardScoring.GroupMidfield),
        [10] = ("Spark", CardScoring.GroupMidfield),
        [11] = ("Magician", CardScoring.GroupAttack),
        [12] = ("Finisher", CardScoring.GroupAttack),
        [13] = ("Target", CardScoring.GroupAttack),
    };
}

/// <summary>Foto imutável do catálogo: entradas editadas no Admin + grupos inferidos dos ids observados.</summary>
public sealed class ArchetypeCatalogSnapshot
{
    public static readonly ArchetypeCatalogSnapshot Empty = new(
        new Dictionary<int, PlayerArchetypeEntity>(), new Dictionary<int, string?>(), available: false, version: 0);

    private readonly Dictionary<int, PlayerArchetypeEntity> _entries;
    private readonly Dictionary<int, string?> _inferred;
    private readonly HashSet<int> _fromDefault = new();
    private readonly ConcurrentDictionary<int, ArchetypeRef> _refs = new();

    /// <param name="inferred">Todos os ids observados nas partidas -> grupo inferido (nulo se nunca houve posição).</param>
    public ArchetypeCatalogSnapshot(
        Dictionary<int, PlayerArchetypeEntity> entries, Dictionary<int, string?> inferred, bool available, long version)
    {
        // Padrão em código para 1..13 quando NÃO há linha no banco (linha existente, mesmo editada/limpa, vence).
        _entries = new Dictionary<int, PlayerArchetypeEntity>(entries);
        foreach (var (id, def) in ArchetypeDefaults.Known)
            if (!_entries.ContainsKey(id))
            {
                _entries[id] = new PlayerArchetypeEntity { Id = (short)id, Name = def.Name, PositionGroup = def.Group };
                _fromDefault.Add(id);
            }
        _inferred = inferred;
        Available = available;
        Version = version;
    }

    /// <summary>False quando a leitura do catálogo falhou (tabela ausente): só rótulos padrão.</summary>
    public bool Available { get; }

    /// <summary>Muda a cada recarga/invalidação; entra nas chaves de cache das análises que embutem rótulos.</summary>
    public long Version { get; }

    /// <summary>True quando o id usa o padrão em código (sem linha no banco).</summary>
    public bool IsDefault(int id) => _fromDefault.Contains(id);

    public PlayerArchetypeEntity? Entry(int id) => _entries.TryGetValue(id, out var e) ? e : null;

    public string? InferredGroup(int id) => _inferred.TryGetValue(id, out var g) ? g : null;

    /// <summary>Referência pronta para o JSON; nulo para id 0/negativo (sem dado).</summary>
    public ArchetypeRef? Ref(int id) => id <= 0 ? null : RefOf(id);

    public ArchetypeRef RefOf(int id) => _refs.GetOrAdd(id, key =>
    {
        _entries.TryGetValue(key, out var e);
        var name = Clean(e?.Name);
        return new ArchetypeRef
        {
            Id = key,
            Name = name,
            Label = name ?? ArchetypeRef.DefaultLabel(key),
            ShortName = Clean(e?.ShortName),
            PositionGroup = ArchetypeGroups.Normalize(e?.PositionGroup) ?? InferredGroup(key)
        };
    });

    /// <summary>Catálogo + ids observados, ordenado por id.</summary>
    public List<ArchetypeRef> All() => _entries.Keys.Concat(_inferred.Keys).Where(id => id > 0).Distinct().OrderBy(id => id)
        .Select(RefOf).ToList();

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // ------------------------------------------------------------------ troca das referências calculadas sem catálogo

    /// <summary>Preenche <c>Archetype</c>/<c>Archetypes</c> de uma linha calculada pelo <c>StatsAggregator</c> (sem catálogo).</summary>
    public void Apply(PlayerStatisticsDto? dto)
    {
        if (dto is null) return;
        dto.Archetype = Ref(dto.ArchetypeId);
        foreach (var u in dto.Archetypes) u.Archetype = RefOf(u.Archetype.Id);
        foreach (var s in dto.Segments) Apply(s);
    }

    public void Apply(IEnumerable<PlayerStatisticsDto>? dtos)
    {
        if (dtos is null) return;
        foreach (var d in dtos) Apply(d);
    }

    public void Apply(MatchPlayerDto? dto)
    {
        if (dto is not null) dto.Archetype = Ref(dto.ArchetypeId);
    }

    public void Apply(MatchPlayerStatsDto? dto)
    {
        if (dto is not null) dto.Archetype = Ref(dto.ArchetypeId);
    }
}

/// <summary>
/// Catálogo de arquétipos com cache de 10 min (singleton; lê o banco por escopo próprio). Resiliência: se a leitura da
/// tabela PlayerArchetypes falhar (ex.: Postgres 42P01 num banco sem a migration — cenário do modo somente leitura),
/// loga UM aviso e passa a servir um catálogo vazio, sem nunca derrubar o endpoint que o chamou.
/// </summary>
public class ArchetypeCatalog : IArchetypeCatalog
{
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan FailureTtl = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ArchetypeCatalog> _logger;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile Entry? _current;
    private long _version;
    private int _warned;

    private sealed record Entry(ArchetypeCatalogSnapshot Snapshot, DateTimeOffset ExpiresAt);

    public ArchetypeCatalog(IServiceScopeFactory scopes, ILogger<ArchetypeCatalog> logger, TimeProvider? time = null)
    {
        _scopes = scopes;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    public void Invalidate() => _current = null;

    public async Task<ArchetypeCatalogSnapshot> GetAsync(CancellationToken ct = default)
    {
        var cur = _current;
        if (cur is not null && _time.GetUtcNow() < cur.ExpiresAt) return cur.Snapshot;

        await _gate.WaitAsync(ct);
        try
        {
            cur = _current;
            if (cur is not null && _time.GetUtcNow() < cur.ExpiresAt) return cur.Snapshot;

            var snapshot = await LoadAsync(ct);
            _current = new Entry(snapshot, _time.GetUtcNow() + (snapshot.Available ? Ttl : FailureTtl));
            return snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Lê as linhas do catálogo. Sobrescrevível para simular falhas nos testes.</summary>
    protected virtual async Task<List<PlayerArchetypeEntity>> ReadEntriesAsync(EAFCContext db, CancellationToken ct) =>
        await db.PlayerArchetypes.AsNoTracking().ToListAsync(ct);

    /// <summary>Posições jogadas por id de arquétipo (uma única consulta agregada).</summary>
    protected virtual async Task<List<(int Id, string Pos, int Count)>> ReadPositionCountsAsync(EAFCContext db, CancellationToken ct)
    {
        var rows = await db.MatchPlayers.AsNoTracking()
            .Where(mp => mp.Archetypeid > 0)
            .GroupBy(mp => new { mp.Archetypeid, mp.Pos })
            .Select(g => new { g.Key.Archetypeid, g.Key.Pos, N = g.Count() })
            .ToListAsync(ct);
        return rows.Select(r => ((int)r.Archetypeid, r.Pos ?? "", r.N)).ToList();
    }

    private async Task<ArchetypeCatalogSnapshot> LoadAsync(CancellationToken ct)
    {
        var entries = new Dictionary<int, PlayerArchetypeEntity>();
        var inferred = new Dictionary<int, string?>();
        var available = true;
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EAFCContext>();

            try
            {
                foreach (var e in await ReadEntriesAsync(db, ct)) entries[e.Id] = e;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                available = false;
                entries.Clear();
                LogFailure(ex, "a tabela PlayerArchetypes");
            }

            try
            {
                foreach (var g in (await ReadPositionCountsAsync(db, ct)).GroupBy(p => p.Id))
                    inferred[g.Key] = ArchetypeGroups.Infer(g.Select(p => (p.Pos, p.Count)));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                available = false;
                inferred.Clear();
                LogFailure(ex, "as posições por arquétipo");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            available = false;
            entries.Clear();
            inferred.Clear();
            LogFailure(ex, "o catálogo");
        }

        return new ArchetypeCatalogSnapshot(entries, inferred, available, Interlocked.Increment(ref _version));
    }

    private void LogFailure(Exception ex, string what)
    {
        var missing = IsMissingTable(ex);
        if (Interlocked.Exchange(ref _warned, 1) == 0)
            _logger.LogWarning(
                "Arquétipos: não foi possível ler {What} ({Reason}); usando catálogo vazio (rótulos \"Arquétipo #id\"). " +
                "Se a tabela PlayerArchetypes não existe neste banco, aplique a migration AddPlayerArchetypes.",
                what, missing ? "tabela inexistente (42P01)" : ex.GetType().Name);
        else
            _logger.LogDebug(ex, "Arquétipos: falha ao ler {What}; catálogo vazio.", what);
    }

    /// <summary>Postgres 42P01 (undefined_table), sem depender do Npgsql: lê SqlState por reflexão nas exceções aninhadas.</summary>
    public static bool IsMissingTable(Exception? ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            var state = e.GetType().GetProperty("SqlState")?.GetValue(e) as string;
            if (state == "42P01") return true;
        }
        return false;
    }
}

/// <summary>Catálogo "vazio" usado como padrão quando um serviço é criado sem catálogo (testes).</summary>
internal sealed class EmptyArchetypeCatalog : IArchetypeCatalog
{
    public static readonly EmptyArchetypeCatalog Instance = new();
    public Task<ArchetypeCatalogSnapshot> GetAsync(CancellationToken ct = default) => Task.FromResult(ArchetypeCatalogSnapshot.Empty);
    public void Invalidate() { }
}
