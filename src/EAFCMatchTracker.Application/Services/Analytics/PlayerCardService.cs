using System.Globalization;
using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace EAFCMatchTracker.Application.Services.Analytics;

/// <summary>Linha por jogador/partida (nosso clube, sem desconectados) com tudo o que as cartas precisam.</summary>
internal sealed record CardRow(
    long MatchId, long PlayerId, string Pos, int Goals, int Assists, int Shots, int PassesMade, int PassAttempts,
    int TacklesMade, int TackleAttempts, int Saves, double Rating, bool Mom, int Reds, int Seconds,
    int? ProOverall, string? ProOverallStr, int? ProHeight, int ArchetypeId = 0);

/// <summary>
/// "Cartas": cartas estilo FUT por jogador (eixos 0–99 em escalas absolutas, overall por posição, faixa) e comparador
/// de dois jogadores. Somente leitura, AsNoTracking, cache de 60 s (a chave inclui o fingerprint do clube, que cobre
/// as configurações de sessão do admin como nos demais serviços analíticos). As fórmulas ficam em <see cref="CardScoring"/>.
/// </summary>
public sealed class PlayerCardService : IPlayerCardService
{
    public const int FormLength = 5;
    public const int RatingSeriesLength = 20;

    private readonly EAFCContext _db;
    private readonly IMemoryCache _cache;
    private readonly ClubAnalyticsLoader _loader;
    private readonly IArchetypeCatalog _archetypes;

    public PlayerCardService(EAFCContext db, IMemoryCache cache, IArchetypeCatalog? archetypes = null)
    {
        _db = db;
        _cache = cache;
        _loader = new ClubAnalyticsLoader(db);
        _archetypes = archetypes ?? EmptyArchetypeCatalog.Instance;
    }

    private static int ClampMin(int minMatches) => Math.Clamp(minMatches, LabService.MinMatchesFloor, LabService.MinMatchesCeiling);

    /// <summary>Tudo o que é lido do banco para um clube/filtro.</summary>
    internal sealed class CardData
    {
        public ClubInfo Info { get; init; } = new();
        public ClubDataset Data { get; init; } = new();
        /// <summary>Linhas consideradas nas cartas (com filtro de arquétipo: só as partidas desse arquétipo).</summary>
        public List<CardRow> Rows { get; init; } = new();
        /// <summary>Todas as linhas do período (sem o filtro de arquétipo); nulo = igual a <see cref="Rows"/>.</summary>
        public List<CardRow>? AllRows { get; init; }
        /// <summary>Linhas com a posição aplicada e SEM o filtro de arquétipo; nulo = igual a <see cref="UsageRows"/>.</summary>
        public List<CardRow>? PositionRows { get; init; }
        public int? ArchetypeFilter { get; init; }
        public string? PositionFilter { get; init; }
        public long? PlayerFilter { get; init; }
        public ArchetypeCatalogSnapshot Catalog { get; init; } = ArchetypeCatalogSnapshot.Empty;

        /// <summary>Todas as linhas do período (sem nenhum filtro de arquétipo/posição).</summary>
        public List<CardRow> UsageRows => AllRows ?? Rows;
        /// <summary>Linhas para o uso de arquétipos: posição aplicada, arquétipo não.</summary>
        public List<CardRow> ScopeRows => PositionRows ?? UsageRows;
        public bool Filtered => ArchetypeFilter.HasValue || PositionFilter is not null;

        /// <summary>
        /// Mesmos dados, restritos às linhas jogadas com o arquétipo e/ou na posição (AND); o resto do período segue
        /// disponível em <see cref="UsageRows"/>/<see cref="ScopeRows"/>.
        /// </summary>
        public CardData WithFilter(int? archetypeId, string? positionGroup, long? playerEntityId = null)
        {
            if (!archetypeId.HasValue && positionGroup is null && !playerEntityId.HasValue) return this;
            var all = UsageRows;
            var scope = ArchetypeUsageCalc.Where(all, r => r.ArchetypeId, r => r.Pos, positionGroup, null).ToList();
            if (playerEntityId.HasValue) scope = scope.Where(r => r.PlayerId == playerEntityId.Value).ToList();
            return new CardData
            {
                Info = Info, Data = Data, Catalog = Catalog, ArchetypeFilter = archetypeId, PositionFilter = positionGroup,
                PlayerFilter = playerEntityId,
                AllRows = all, PositionRows = scope,
                Rows = archetypeId.HasValue ? scope.Where(r => r.ArchetypeId == archetypeId.Value).ToList() : scope
            };
        }

        public CardData WithCatalog(ArchetypeCatalogSnapshot catalog) => new()
        {
            Info = Info, Data = Data, Rows = Rows, AllRows = AllRows, PositionRows = PositionRows,
            ArchetypeFilter = ArchetypeFilter, PositionFilter = PositionFilter, PlayerFilter = PlayerFilter, Catalog = catalog
        };
    }

    private Task<CardData> LoadAsync(long clubId, DateOnly? from, DateOnly? to, int? gameVersion, CancellationToken ct) =>
        LoadCardDataAsync(_db, _loader, clubId, from, to, gameVersion, ct);

    /// <summary>Carrega as linhas jogador×partida do clube (sem desconectados). Compartilhado com o resumo de arquétipos.</summary>
    internal static async Task<CardData> LoadCardDataAsync(
        EAFCContext db, ClubAnalyticsLoader loader, long clubId, DateOnly? from, DateOnly? to, int? gameVersion, CancellationToken ct)
    {
        var info = await loader.GetClubInfoAsync(clubId, ct);
        var (known, versionId, _) = await loader.ResolveVersionAsync(gameVersion, ct);
        if (!known) return new CardData { Info = info };

        DateTime? fromUtc = from.HasValue ? StatsUtil.LocalDayStartUtc(from.Value, info.Zone) : null;
        DateTime? toUtc = to.HasValue ? StatsUtil.LocalDayStartUtc(to.Value.AddDays(1), info.Zone) : null;
        var data = await loader.LoadAsync(clubId, versionId, fromUtc, toUtc, null, DataParts.Players | DataParts.GoalLinks, ct);
        if (data.Matches.Count == 0) return new CardData { Info = info, Data = data };

        var ids = data.Matches.Select(m => m.MatchId).ToList();
        // Mesma regra das páginas de estatística (StatsAggregator): jogadores desconectados ficam de fora.
        var rows = (await db.MatchPlayers.AsNoTracking()
                .Where(mp => mp.ClubId == clubId && !mp.Disconnected && ids.Contains(mp.MatchId))
                .Select(mp => new
                {
                    mp.MatchId, mp.PlayerEntityId, mp.Pos, mp.Goals, mp.Assists, mp.Shots, mp.Passesmade, mp.Passattempts,
                    mp.Tacklesmade, mp.Tackleattempts, mp.Saves, mp.Rating, mp.Mom, mp.Redcards, mp.SecondsPlayed,
                    mp.ProOverall, mp.ProOverallStr, mp.ProHeight, mp.Archetypeid
                })
                .ToListAsync(ct))
            .Select(x => new CardRow(x.MatchId, x.PlayerEntityId, (x.Pos ?? "").Trim(), x.Goals, x.Assists, x.Shots,
                x.Passesmade, x.Passattempts, x.Tacklesmade, x.Tackleattempts, x.Saves,
                double.IsNaN(x.Rating) ? 0 : x.Rating, x.Mom, x.Redcards, x.SecondsPlayed,
                x.ProOverall, x.ProOverallStr, x.ProHeight, x.Archetypeid))
            .ToList();
        return new CardData { Info = info, Data = data, Rows = rows };
    }

    // ------------------------------------------------------------------------------------------ cards

    public Task<PlayerCardsDto> GetCardsAsync(
        long clubId, DateOnly? from, DateOnly? to, int? gameVersion, int minMatches, CancellationToken ct) =>
        GetCardsAsync(clubId, from, to, gameVersion, minMatches, null, null, ct);

    public Task<PlayerCardsDto> GetCardsAsync(
        long clubId, DateOnly? from, DateOnly? to, int? gameVersion, int minMatches, int? archetypeId, CancellationToken ct) =>
        GetCardsAsync(clubId, from, to, gameVersion, minMatches, archetypeId, null, ct);

    public Task<PlayerCardsDto> GetCardsAsync(
        long clubId, DateOnly? from, DateOnly? to, int? gameVersion, int minMatches, int? archetypeId, string? positionGroup,
        CancellationToken ct) =>
        GetCardsAsync(clubId, from, to, gameVersion, minMatches, archetypeId, positionGroup, ViewPlayer, ct);

    public const string ViewPlayer = "player", ViewArchetype = "archetype";

    public Task<PlayerCardsDto> GetCardsAsync(
        long clubId, DateOnly? from, DateOnly? to, int? gameVersion, int minMatches, int? archetypeId, string? positionGroup,
        string? view, CancellationToken ct) =>
        GetCardsAsync(clubId, from, to, gameVersion, minMatches, archetypeId, positionGroup, view, null, ct);

    public async Task<PlayerCardsDto> GetCardsAsync(
        long clubId, DateOnly? from, DateOnly? to, int? gameVersion, int minMatches, int? archetypeId, string? positionGroup,
        string? view, long? playerEntityId, CancellationToken ct)
    {
        view = string.Equals(view?.Trim(), ViewArchetype, StringComparison.OrdinalIgnoreCase) ? ViewArchetype : ViewPlayer;
        minMatches = ClampMin(minMatches);
        positionGroup = ArchetypeGroups.Normalize(positionGroup);
        var catalog = await _archetypes.GetAsync(ct);
        return await AnalyticsCache.GetOrCreateAsync(_cache, _loader, "player-cards", clubId,
            $"{from:yyyy-MM-dd}:{to:yyyy-MM-dd}:v={gameVersion}:m={minMatches}:arq={archetypeId}:pos={positionGroup}:view={view}:pl={playerEntityId}:c{catalog.Version}", async () =>
            {
                var d = (await LoadAsync(clubId, from, to, gameVersion, ct)).WithCatalog(catalog);
                return BuildCards(clubId, from, to, gameVersion, minMatches, d, archetypeId, positionGroup, view, playerEntityId);
            }, ct);
    }

    /// <summary>Arquétipos do período COM a posição aplicada e SEM o filtro de arquétipo, para montar o 2º filtro da tela.</summary>
    internal static List<AvailableArchetypeDto> AvailableArchetypes(CardData d, string? positionGroup = null, long? playerEntityId = null) =>
        ArchetypeUsageCalc.AvailableArchetypes(
            playerEntityId.HasValue ? d.UsageRows.Where(r => r.PlayerId == playerEntityId.Value).ToList() : d.UsageRows, r => r.ArchetypeId, r => r.PlayerId, r => r.Pos, positionGroup, d.Catalog.RefOf);

    internal static PlayerCardsDto BuildCards(
        long clubId, DateOnly? from, DateOnly? to, int? gameVersion, int minMatches, CardData d,
        int? archetypeId = null, string? positionGroup = null, string view = ViewPlayer, long? playerEntityId = null)
    {
        // opções dos filtros: cada uma calculada sem o PRÓPRIO filtro (e sem os que vêm depois dela na cadeia posição -> arquétipo)
        var available = AvailableArchetypes(d, positionGroup, playerEntityId);
        var positions = ArchetypeUsageCalc.AvailablePositionGroups(
            playerEntityId.HasValue ? d.UsageRows.Where(r => r.PlayerId == playerEntityId.Value).ToList() : d.UsageRows, r => r.Pos);
        var players = d.UsageRows
            .GroupBy(r => r.PlayerId)
            .Select(g => new AvailablePlayerDto { PlayerEntityId = g.Key, Name = d.Data.NameOf(g.Key), Matches = g.Count() })
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.PlayerEntityId).ToList();
        d = d.WithFilter(archetypeId, positionGroup, playerEntityId);
        // jogador escolhido: minMatches não esconde as cartas dele (senão um jogador com poucos jogos abre uma grade vazia)
        var effectiveMin = playerEntityId.HasValue ? 1 : minMatches;
        var segmented = view == ViewArchetype;
        var cards = segmented ? BuildSegmentCards(d, null) : BuildAllCards(d, null).Values.ToList();
        return new PlayerCardsDto
        {
            ClubId = clubId,
            ClubName = d.Info.Name,
            From = from,
            To = to,
            GameVersion = gameVersion,
            TotalMatches = d.Data.Matches.Count,
            MinMatches = minMatches,
            Cards = OrderCards(cards.Where(c => c.Matches >= effectiveMin)),
            PlayerEntityId = playerEntityId,
            AvailablePlayers = players,
            ArchetypeId = archetypeId,
            View = view,
            PositionGroup = positionGroup,
            AvailableArchetypes = available,
            AvailablePositionGroups = positions
        };
    }

    internal static List<PlayerCardDto> OrderCards(IEnumerable<PlayerCardDto> cards) => cards
        .OrderByDescending(c => c.Overall).ThenByDescending(c => c.Matches)
        .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.PlayerEntityId)
        .ThenBy(c => c.SegmentKey, StringComparer.Ordinal)
        .ToList();

    /// <summary>Uma carta por jogador que jogou no período (mais, opcionalmente, jogadores sem jogos: carta zerada).</summary>
    internal static Dictionary<long, PlayerCardDto> BuildAllCards(CardData d, IReadOnlyDictionary<long, string>? extraNames)
    {
        var matchesById = d.Data.Matches.ToDictionary(m => m.MatchId);
        var preAssistGoals = d.Data.Goals.Where(g => g.PreAssistId.HasValue).ToLookup(g => g.PreAssistId!.Value);
        var allRowsByPlayer = d.ScopeRows.ToLookup(r => r.PlayerId);

        var result = new Dictionary<long, PlayerCardDto>();
        foreach (var g in d.Rows.GroupBy(r => r.PlayerId))
        {
            var name = d.Data.Names.TryGetValue(g.Key, out var n) ? n
                : extraNames is not null && extraNames.TryGetValue(g.Key, out var e) ? e : d.Data.NameOf(g.Key);
            var rows = g.ToList();
            result[g.Key] = BuildCard(g.Key, name, rows, d.Data.Matches.Count, matchesById,
                PreAssistsOf(d, preAssistGoals, g.Key, rows), ArchetypeInfo(d, matchesById, allRowsByPlayer[g.Key].ToList(), rows),
                scoringArchetypeId: d.ArchetypeFilter);
        }
        return result;
    }

    /// <summary>
    /// <c>view=archetype</c>: uma carta por (jogador, arquétipo), cada uma calculada SÓ com as partidas desse segmento
    /// (id 0/sem dado forma um segmento com arquétipo nulo e pesos da posição). Overall com os pesos do arquétipo quando o id
    /// está na tabela de <see cref="CardScoring.ArchetypeWeights"/>. O mínimo de jogos e a regra de provisória valem por segmento.
    /// </summary>
    internal static List<PlayerCardDto> BuildSegmentCards(CardData d, IReadOnlyDictionary<long, string>? extraNames)
    {
        var matchesById = d.Data.Matches.ToDictionary(m => m.MatchId);
        var preAssistGoals = d.Data.Goals.Where(g => g.PreAssistId.HasValue).ToLookup(g => g.PreAssistId!.Value);
        var scopeByPlayer = d.ScopeRows.ToLookup(r => r.PlayerId);

        var result = new List<PlayerCardDto>();
        foreach (var player in d.Rows.GroupBy(r => r.PlayerId))
        {
            var name = d.Data.Names.TryGetValue(player.Key, out var n) ? n
                : extraNames is not null && extraNames.TryGetValue(player.Key, out var e) ? e : d.Data.NameOf(player.Key);
            var usages = ArchetypeInfo(d, matchesById, scopeByPlayer[player.Key].ToList(), new List<CardRow>()).Usages;
            foreach (var segment in player.GroupBy(r => r.ArchetypeId > 0 ? r.ArchetypeId : 0))
            {
                var rows = segment.ToList();
                var matchIds = rows.Select(r => r.MatchId).ToHashSet();
                var pre = preAssistGoals[player.Key].Count(g => matchIds.Contains(g.MatchId));
                var archetype = segment.Key > 0 ? d.Catalog.RefOf(segment.Key) : null;
                var card = BuildCard(player.Key, name, rows, d.Data.Matches.Count, matchesById, pre, (archetype, usages),
                    scoringArchetypeId: segment.Key > 0 ? segment.Key : null);
                card.SegmentKey = $"{player.Key}-{segment.Key}";
                result.Add(card);
            }
        }
        return result;
    }

    /// <summary>
    /// Pré-assistências do jogador. Sem filtro de arquétipo vale o total do período (comportamento original); com filtro,
    /// só as dos gols das partidas em que ele jogou com aquele arquétipo.
    /// </summary>
    private static int PreAssistsOf(CardData d, ILookup<long, GoalLinkRow> goals, long playerId, List<CardRow> rows)
    {
        if (!d.Filtered) return goals[playerId].Count();
        var matchIds = rows.Select(r => r.MatchId).ToHashSet();
        return goals[playerId].Count(g => matchIds.Contains(g.MatchId));
    }

    private static (ArchetypeRef? Principal, List<ArchetypeUsage> Usages) ArchetypeInfo(
        CardData d, IReadOnlyDictionary<long, MatchRow> matchesById, List<CardRow> allRows, List<CardRow> rows)
    {
        ArchetypeSample Sample(CardRow r) => new(
            r.ArchetypeId, matchesById.TryGetValue(r.MatchId, out var m) ? m.Timestamp : DateTime.MinValue, r.MatchId,
            r.Rating, r.ProOverall, r.ProOverallStr, r.Goals, r.Assists);

        var usages = ArchetypeUsageCalc.Usages(allRows.Select(Sample).ToList(), d.Catalog.RefOf);
        var principal = d.Filtered
            ? ArchetypeUsageCalc.Principal(ArchetypeUsageCalc.Usages(rows.Select(Sample).ToList(), d.Catalog.RefOf))
            : ArchetypeUsageCalc.Principal(usages);
        return (principal, usages);
    }

    internal static PlayerCardDto BuildCard(
        long playerId, string name, List<CardRow> rows, int totalMatches,
        IReadOnlyDictionary<long, MatchRow> matchesById, int preAssists,
        (ArchetypeRef? Principal, List<ArchetypeUsage> Usages)? archetype = null, int? scoringArchetypeId = null)
    {
        var n = rows.Count;
        if (n == 0)
        {
            // Jogador do clube sem jogos no filtro (só o comparador chega aqui): carta zerada, sem eixos informativos.
            return new PlayerCardDto
            {
                PlayerEntityId = playerId, Name = name, Position = "", PositionGroup = CardScoring.GroupMidfield,
                Matches = 0, Minutes = 0, Tier = CardScoring.TierBronze, Overall = 0, Provisional = true,
                Axes = new PlayerCardAxesDto { Ata = 0 },
                Archetype = archetype?.Principal,
                Archetypes = archetype?.Usages ?? new List<ArchetypeUsage>()
            };
        }

        DateTime TimeOf(CardRow r) => matchesById.TryGetValue(r.MatchId, out var m) ? m.Timestamp : DateTime.MinValue;
        var ordered = rows.OrderByDescending(TimeOf).ThenByDescending(r => r.MatchId).ToList();

        // Posição: a mais frequente; empate vai para a mais recente (ordered já é do mais novo para o mais antigo).
        var positionRows = ordered.Where(r => r.Pos.Length > 0).ToList();
        var position = positionRows
            .Select((r, i) => (r.Pos, Index: i))
            .GroupBy(x => x.Pos, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Count()).ThenBy(x => x.Min(y => y.Index))
            .Select(x => x.First().Pos).FirstOrDefault() ?? "";
        var group = CardScoring.PositionGroup(position);
        var keeper = group == CardScoring.GroupKeeper;

        int goals = rows.Sum(r => r.Goals), assists = rows.Sum(r => r.Assists), shots = rows.Sum(r => r.Shots);
        int passesMade = rows.Sum(r => r.PassesMade), passAttempts = rows.Sum(r => r.PassAttempts);
        int tacklesMade = rows.Sum(r => r.TacklesMade), tackleAttempts = rows.Sum(r => r.TackleAttempts);
        var motm = rows.Count(r => r.Mom);
        var reds = rows.Sum(r => r.Reds);

        // Mesmas definições de precisão das páginas de estatística (StatsAggregator).
        var shotAcc = shots > 0 ? goals * 100.0 / shots : 0;
        var passAcc = passAttempts > 0 ? passesMade * 100.0 / passAttempts : 0;
        var tackleAcc = tackleAttempts > 0 ? tacklesMade * 100.0 / tackleAttempts : 0;

        var ratings = rows.Select(r => r.Rating).ToList();
        var avgRating = ratings.Average();
        var stdDev = Math.Sqrt(ratings.Sum(x => (x - avgRating) * (x - avgRating)) / n);

        var gpm = goals / (double)n;
        var apm = assists / (double)n;

        var raw = new
        {
            Ata = CardScoring.RawAta(gpm, shotAcc),
            Pas = CardScoring.RawPas(passAcc, passesMade / (double)n),
            Cri = CardScoring.RawCri(apm, preAssists / (double)n),
            Def = CardScoring.RawDef(tackleAcc, tacklesMade / (double)n),
            Imp = CardScoring.RawImp(avgRating, motm / (double)n),
            // Regularidade = só estabilidade das notas das linhas da própria carta (sem presença; ver CardScoring.RawReg)
            Reg = CardScoring.RawReg(stdDev, n)
        };

        // Goleiro: defesas/gols sofridos/jogos sem sofrer gol contam só as partidas jogadas na posição de goleiro
        // (gols sofridos = gols contra do clube na partida); sem nenhuma, usa todas.
        double? savePct = null, gol = null;
        int? cleanSheets = null;
        if (keeper)
        {
            var gk = rows.Where(r => CardScoring.PositionGroup(r.Pos) == CardScoring.GroupKeeper).ToList();
            if (gk.Count == 0) gk = rows;
            var conceded = gk.Sum(r => matchesById.TryGetValue(r.MatchId, out var m) ? m.Ga : 0);
            var saves = gk.Sum(r => r.Saves);
            var faced = saves + conceded;
            savePct = faced > 0 ? saves * 100.0 / faced : 0;
            cleanSheets = gk.Count(r => matchesById.TryGetValue(r.MatchId, out var m) && m.Ga == 0);
            gol = CardScoring.Score(CardScoring.RawGol(savePct.Value, conceded / (double)gk.Count), n);
        }

        var axes = new CardAxes(
            keeper ? null : CardScoring.Score(raw.Ata, n),
            CardScoring.Score(raw.Pas, n), CardScoring.Score(raw.Cri, n), CardScoring.Score(raw.Def, n),
            CardScoring.Score(raw.Imp, n), CardScoring.Score(raw.Reg, n), gol);
        // Pesos por arquétipo só quando a carta usa partidas de UM arquétipo (scoringArchetypeId) e o id está na tabela.
        // provisória (< 10 jogos): nunca elite (teto 84), nos dois overalls
        var overallByPosition = CardScoring.CapProvisional(CardScoring.Overall(group, axes), n);
        var byArchetype = CardScoring.UsesArchetypeWeights(scoringArchetypeId, keeper);
        var overall = byArchetype ? CardScoring.CapProvisional(CardScoring.Overall(group, axes, scoringArchetypeId), n) : overallByPosition;

        int? attrOverall = ordered.Select(r => r.ProOverall ?? ParseInt(r.ProOverallStr)).FirstOrDefault(v => v.HasValue);
        int? attrHeight = ordered.Select(r => r.ProHeight).FirstOrDefault(v => v is > 0);
        var attributes = attrOverall.HasValue || attrHeight.HasValue
            ? new PlayerCardAttributesDto
            {
                Overall = attrOverall,
                Height = attrHeight.HasValue ? $"{attrHeight.Value.ToString(CultureInfo.InvariantCulture)} cm" : null
            }
            : null;

        return new PlayerCardDto
        {
            PlayerEntityId = playerId,
            Name = name,
            Position = position,
            PositionGroup = group,
            Matches = n,
            Minutes = (int)Math.Round(rows.Sum(r => (long)r.Seconds) / 60.0, MidpointRounding.AwayFromZero),
            Tier = CardScoring.Tier(overall),
            Overall = overall,
            OverallByPosition = overallByPosition,
            Scoring = byArchetype ? CardScoring.ScoringArchetype : CardScoring.ScoringPosition,
            Provisional = CardScoring.IsProvisional(n),
            Axes = new PlayerCardAxesDto
            {
                Ata = axes.Ata.HasValue ? RoundAxis(axes.Ata.Value) : null,
                Pas = RoundAxis(axes.Pas),
                Cri = RoundAxis(axes.Cri),
                Def = RoundAxis(axes.Def),
                Imp = RoundAxis(axes.Imp),
                Reg = RoundAxis(axes.Reg),
                Gol = axes.Gol.HasValue ? RoundAxis(axes.Gol.Value) : null
            },
            Stats = new PlayerCardStatsDto
            {
                Goals = goals,
                Assists = assists,
                PreAssists = preAssists,
                GoalsPerMatch = StatsUtil.Round2(gpm),
                AssistsPerMatch = StatsUtil.Round2(apm),
                ShotAccuracyPct = StatsUtil.Round2(shotAcc),
                PassAccuracyPct = StatsUtil.Round2(passAcc),
                TackleAccuracyPct = StatsUtil.Round2(tackleAcc),
                SavePct = savePct.HasValue ? StatsUtil.Round2(savePct.Value) : null,
                AvgRating = StatsUtil.Round2(avgRating),
                Motm = motm,
                RedCards = reds,
                CleanSheets = cleanSheets
            },
            Form = ordered.Take(FormLength).Select(r => r.Rating).ToList(),
            Attributes = attributes,
            LastPlayedAt = matchesById.TryGetValue(ordered[0].MatchId, out var last) ? last.Timestamp : null,
            Archetype = archetype?.Principal,
            Archetypes = archetype?.Usages ?? new List<ArchetypeUsage>()
        };
    }

    private static int RoundAxis(double v) => (int)Math.Clamp(Math.Round(v, MidpointRounding.AwayFromZero), 0, CardScoring.MaxScore);

    private static int? ParseInt(string? s) =>
        int.TryParse(s?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : null;

    // ------------------------------------------------------------------------------------------ compare

    public Task<PlayerCompareDto?> GetCompareAsync(
        long clubId, long a, long b, DateOnly? from, DateOnly? to, int? gameVersion, CancellationToken ct) =>
        GetCompareAsync(clubId, a, b, from, to, gameVersion, null, null, ct);

    public Task<PlayerCompareDto?> GetCompareAsync(
        long clubId, long a, long b, DateOnly? from, DateOnly? to, int? gameVersion, int? archetypeId, CancellationToken ct) =>
        GetCompareAsync(clubId, a, b, from, to, gameVersion, archetypeId, null, ct);

    public Task<PlayerCompareDto?> GetCompareAsync(
        long clubId, long a, long b, DateOnly? from, DateOnly? to, int? gameVersion, int? archetypeId, string? positionGroup,
        CancellationToken ct) =>
        GetCompareAsync(clubId, a, b, from, to, gameVersion, archetypeId, null, null, positionGroup, ct);

    public async Task<PlayerCompareDto?> GetCompareAsync(
        long clubId, long a, long b, DateOnly? from, DateOnly? to, int? gameVersion, int? archetypeId, int? archetypeA, int? archetypeB,
        string? positionGroup, CancellationToken ct)
    {
        positionGroup = ArchetypeGroups.Normalize(positionGroup);
        var nameA = await ClubPlayerNameAsync(clubId, a, ct);
        var nameB = await ClubPlayerNameAsync(clubId, b, ct);
        if (nameA is null || nameB is null) return null;

        var catalog = await _archetypes.GetAsync(ct);
        return await AnalyticsCache.GetOrCreateAsync(_cache, _loader, "player-compare", clubId,
            $"a={a}:b={b}:{from:yyyy-MM-dd}:{to:yyyy-MM-dd}:v={gameVersion}:arq={archetypeId}:aa={archetypeA}:ab={archetypeB}:pos={positionGroup}:c{catalog.Version}", async () =>
            {
                var d = (await LoadAsync(clubId, from, to, gameVersion, ct)).WithCatalog(catalog);
                return BuildCompare(clubId, a, b, new Dictionary<long, string> { [a] = nameA, [b] = nameB }, d, archetypeId, positionGroup,
                    archetypeA, archetypeB);
            }, ct);
    }

    /// <summary>Nome do jogador se ele pertence ao clube (entidade do clube ou alguma partida pelo clube); senão nulo.</summary>
    private async Task<string?> ClubPlayerNameAsync(long clubId, long playerEntityId, CancellationToken ct)
    {
        var player = await _db.Players.AsNoTracking().Where(p => p.Id == playerEntityId)
            .Select(p => new { p.ClubId, p.Playername }).FirstOrDefaultAsync(ct);
        var belongs = player?.ClubId == clubId
            || await _db.MatchPlayers.AsNoTracking().AnyAsync(mp => mp.PlayerEntityId == playerEntityId && mp.ClubId == clubId, ct);
        if (!belongs) return null;
        if (!string.IsNullOrWhiteSpace(player?.Playername)) return player!.Playername.Trim();
        var pro = await _db.MatchPlayers.AsNoTracking().Where(mp => mp.PlayerEntityId == playerEntityId && mp.ProName != null)
            .Select(mp => mp.ProName).FirstOrDefaultAsync(ct);
        return !string.IsNullOrWhiteSpace(pro) ? pro!.Trim() : $"Jogador {playerEntityId}";
    }

    internal static PlayerCompareDto BuildCompare(
        long clubId, long a, long b, IReadOnlyDictionary<long, string> names, CardData d, int? archetypeId = null,
        string? positionGroup = null, int? archetypeA = null, int? archetypeB = null)
    {
        // Cada lado pode ser comparado COMO um arquétipo específico (archetypeA/archetypeB); sem eles vale archetypeId para os dois.
        var archA = archetypeA ?? archetypeId;
        var archB = archetypeB ?? archetypeId;
        var dA = d.WithFilter(archA, positionGroup);
        var dB = archB == archA ? dA : d.WithFilter(archB, positionGroup);
        var matchesById = d.Data.Matches.ToDictionary(m => m.MatchId);
        var preAssists = d.Data.Goals.Where(g => g.PreAssistId.HasValue)
            .GroupBy(g => g.PreAssistId!.Value).ToDictionary(g => g.Key, g => g.Count());

        PlayerCardDto CardOf(CardData side, Dictionary<long, PlayerCardDto> cards, long id) => cards.TryGetValue(id, out var c)
            ? c
            : BuildCard(id, names[id], new List<CardRow>(), d.Data.Matches.Count, matchesById, preAssists.GetValueOrDefault(id),
                ArchetypeInfo(side, matchesById, side.ScopeRows.Where(r => r.PlayerId == id).ToList(), new List<CardRow>()));
        var cardsA = BuildAllCards(dA, names);
        var cardsB = ReferenceEquals(dA, dB) ? cardsA : BuildAllCards(dB, names);
        var cardA = CardOf(dA, cardsA, a);
        var cardB = CardOf(dB, cardsB, b);

        var ratingA = dA.Rows.Where(r => r.PlayerId == a).GroupBy(r => r.MatchId).ToDictionary(g => g.Key, g => g.First().Rating);
        var ratingB = dB.Rows.Where(r => r.PlayerId == b).GroupBy(r => r.MatchId).ToDictionary(g => g.Key, g => g.First().Rating);

        var together = d.Data.Matches.Where(m => ratingA.ContainsKey(m.MatchId) && ratingB.ContainsKey(m.MatchId)).ToList();
        var onlyA = d.Data.Matches.Where(m => ratingA.ContainsKey(m.MatchId) && !ratingB.ContainsKey(m.MatchId)).ToList();
        var onlyB = d.Data.Matches.Where(m => !ratingA.ContainsKey(m.MatchId) && ratingB.ContainsKey(m.MatchId)).ToList();

        var series = d.Data.Matches
            .Where(m => ratingA.ContainsKey(m.MatchId) || ratingB.ContainsKey(m.MatchId))
            .OrderByDescending(m => m.Timestamp).ThenByDescending(m => m.MatchId)
            .Take(RatingSeriesLength)
            .OrderBy(m => m.Timestamp).ThenBy(m => m.MatchId)
            .Select(m => new PlayerRatingPointDto
            {
                MatchId = m.MatchId,
                Timestamp = m.Timestamp,
                A = ratingA.TryGetValue(m.MatchId, out var ra) ? ra : null,
                B = ratingB.TryGetValue(m.MatchId, out var rb) ? rb : null
            })
            .ToList();

        return new PlayerCompareDto
        {
            ClubId = clubId,
            ArchetypeId = archetypeId,
            ArchetypeIdA = archA,
            ArchetypeIdB = archB,
            PositionGroup = positionGroup,
            A = cardA,
            B = cardB,
            Metrics = BuildMetrics(cardA, cardB),
            Together = together.Count > 0 ? StatsUtil.Stats(together) : null,
            OnlyA = onlyA.Count > 0 ? StatsUtil.Stats(onlyA) : null,
            OnlyB = onlyB.Count > 0 ? StatsUtil.Stats(onlyB) : null,
            RatingSeries = series
        };
    }

    internal static string? Winner(double? a, double? b, bool higherIsBetter)
    {
        if (a is null || b is null) return null;
        if (a.Value == b.Value) return "tie";
        return (a.Value > b.Value) == higherIsBetter ? "a" : "b";
    }

    internal static List<PlayerCompareMetricDto> BuildMetrics(PlayerCardDto a, PlayerCardDto b)
    {
        var metrics = new List<PlayerCompareMetricDto>();

        // Quem não tem jogos no período não tem valor (nulo), em vez de zeros enganosos.
        double? V(PlayerCardDto c, double? value) => c.Matches > 0 ? value : null;

        void Add(string key, string label, double? va, double? vb, bool higherIsBetter = true) => metrics.Add(new PlayerCompareMetricDto
        {
            Key = key,
            Label = label,
            A = V(a, va),
            B = V(b, vb),
            HigherIsBetter = higherIsBetter,
            Winner = Winner(V(a, va), V(b, vb), higherIsBetter)
        });

        Add("overall", "Geral", a.Overall, b.Overall);
        // Ataque é nulo para goleiros e Goleiro só existe para goleiros: cada linha aparece se pelo menos um lado a tem.
        if (a.Axes.Ata.HasValue || b.Axes.Ata.HasValue) Add("ata", "Ataque", a.Axes.Ata, b.Axes.Ata);
        if (a.Axes.Gol.HasValue || b.Axes.Gol.HasValue) Add("gol", "Goleiro", a.Axes.Gol, b.Axes.Gol);
        Add("pas", "Passe", a.Axes.Pas, b.Axes.Pas);
        Add("cri", "Criação", a.Axes.Cri, b.Axes.Cri);
        Add("def", "Defesa", a.Axes.Def, b.Axes.Def);
        Add("imp", "Impacto", a.Axes.Imp, b.Axes.Imp);
        Add("reg", "Regularidade", a.Axes.Reg, b.Axes.Reg);
        Add("goalsPerMatch", "Gols por jogo", a.Stats.GoalsPerMatch, b.Stats.GoalsPerMatch);
        Add("assistsPerMatch", "Assistências por jogo", a.Stats.AssistsPerMatch, b.Stats.AssistsPerMatch);
        Add("avgRating", "Nota média", a.Stats.AvgRating, b.Stats.AvgRating);
        Add("shotAccuracyPct", "Precisão de chutes (%)", a.Stats.ShotAccuracyPct, b.Stats.ShotAccuracyPct);
        Add("passAccuracyPct", "Precisão de passes (%)", a.Stats.PassAccuracyPct, b.Stats.PassAccuracyPct);
        Add("tackleAccuracyPct", "Desarmes certos (%)", a.Stats.TackleAccuracyPct, b.Stats.TackleAccuracyPct);
        Add("motm", "Melhor em campo", a.Stats.Motm, b.Stats.Motm);
        Add("redCards", "Cartões vermelhos", a.Stats.RedCards, b.Stats.RedCards, higherIsBetter: false);
        return metrics;
    }
}
