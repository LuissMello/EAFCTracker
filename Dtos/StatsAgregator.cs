using System;
using System.Collections.Generic;
using System.Linq;

namespace EAFCMatchTracker.Dtos
{
    public static class StatsAggregator
    {
        public static MatchStatisticsDto BuildOverallForSingleMatch(ICollection<MatchPlayerEntity> players)
        {
            if (players == null || players.Count == 0)
                return new MatchStatisticsDto { TotalMatches = 1 };

            var data = players.Where(p => p != null && !p.Disconnected).ToList();

            if (data.Count == 0)
                return new MatchStatisticsDto
                {
                    TotalMatches = 1,
                    TotalPlayers = 0
                };

            int totalPlayers = data.Count;
            int goals = data.Sum(p => p.Goals);
            int assists = data.Sum(p => p.Assists);
            int shots = data.Sum(p => p.Shots);
            int passesMade = data.Sum(p => p.Passesmade);
            int passAttempts = data.Sum(p => p.Passattempts);
            int tacklesMade = data.Sum(p => p.Tacklesmade);
            int tackleAttempts = data.Sum(p => p.Tackleattempts);
            double rating = data.Sum(p => p.Rating);
            int wins = data.Sum(p => p.Wins);
            int losses = data.Sum(p => p.Losses);
            int cleanSheets = data.Sum(p => p.Cleansheetsany);
            int red = data.Sum(p => p.Redcards);
            int saves = data.Sum(p => p.Saves);
            int mom = data.Count(p => p.Mom);
            int draws = totalPlayers - wins - losses;

            return new MatchStatisticsDto
            {
                TotalMatches = 1,
                TotalPlayers = totalPlayers,

                TotalGoals = goals,
                TotalAssists = assists,
                TotalShots = shots,
                TotalPassesMade = passesMade,
                TotalPassAttempts = passAttempts,
                TotalTacklesMade = tacklesMade,
                TotalTackleAttempts = tackleAttempts,
                TotalRating = rating,

                TotalWins = wins,
                TotalLosses = losses,
                TotalDraws = draws,
                TotalCleanSheets = cleanSheets,
                TotalRedCards = red,
                TotalSaves = saves,
                TotalMom = mom,

                AvgGoals = totalPlayers > 0 ? goals / (double)totalPlayers : 0,
                AvgAssists = totalPlayers > 0 ? assists / (double)totalPlayers : 0,
                AvgShots = totalPlayers > 0 ? shots / (double)totalPlayers : 0,
                AvgPassesMade = totalPlayers > 0 ? passesMade / (double)totalPlayers : 0,
                AvgPassAttempts = totalPlayers > 0 ? passAttempts / (double)totalPlayers : 0,
                AvgTacklesMade = totalPlayers > 0 ? tacklesMade / (double)totalPlayers : 0,
                AvgTackleAttempts = totalPlayers > 0 ? tackleAttempts / (double)totalPlayers : 0,
                AvgRating = totalPlayers > 0 ? rating / totalPlayers : 0,
                AvgRedCards = totalPlayers > 0 ? red / (double)totalPlayers : 0,
                AvgSaves = totalPlayers > 0 ? saves / (double)totalPlayers : 0,
                AvgMom = totalPlayers > 0 ? mom / (double)totalPlayers : 0,

                WinPercent = totalPlayers > 0 ? wins * 100.0 / totalPlayers : 0,
                LossPercent = totalPlayers > 0 ? losses * 100.0 / totalPlayers : 0,
                DrawPercent = totalPlayers > 0 ? draws * 100.0 / totalPlayers : 0,
                CleanSheetsPercent = totalPlayers > 0 ? cleanSheets * 100.0 / totalPlayers : 0,
                MomPercent = totalPlayers > 0 ? mom * 100.0 / totalPlayers : 0,
                PassAccuracyPercent = passAttempts > 0 ? passesMade * 100.0 / passAttempts : 0,
                TackleSuccessPercent = tackleAttempts > 0 ? tacklesMade * 100.0 / tackleAttempts : 0,
                GoalAccuracyPercent = shots > 0 ? goals * 100.0 / shots : 0
            };
        }

        public static List<PlayerStatisticsDto> BuildPerPlayer(
            IEnumerable<MatchPlayerEntity> players,
            bool includeDisconnected = false) 
        {
            if (players == null) return new List<PlayerStatisticsDto>();

            var src = players.Where(p => p != null && (includeDisconnected || !p.Disconnected));

            return src
                .GroupBy(p => p.PlayerEntityId)
                .Select(g =>
                {
                    var player = g.First().Player;
                    var matchPlayer = g.First();

                    int matches = g.Count();
                    int goals = g.Sum(p => p.Goals);
                    int shots = g.Sum(p => p.Shots);
                    int passesMade = g.Sum(p => p.Passesmade);
                    int passAttempts = g.Sum(p => p.Passattempts);
                    int tacklesMade = g.Sum(p => p.Tacklesmade);
                    int tackleAttempts = g.Sum(p => p.Tackleattempts);
                    int wins = g.Sum(p => p.Wins);
                    int losses = g.Sum(p => p.Losses);
                    int draws = matches - wins - losses;

                    return new PlayerStatisticsDto
                    {
                        PlayerId = g.Key,
                        PlayerName = player?.Playername ?? "Unknown",
                        ClubId = player?.ClubId ?? 0,
                        ProHeight = matchPlayer.ProHeight,
                        ProName = matchPlayer.ProName,
                        ProOverallStr = matchPlayer.ProOverallStr,

                        MatchesPlayed = matches,
                        TotalGoals = goals,
                        TotalAssists = g.Sum(p => p.Assists),
                        TotalShots = shots,
                        TotalPassesMade = passesMade,
                        TotalPassAttempts = passAttempts,
                        TotalTacklesMade = tacklesMade,
                        TotalTackleAttempts = tackleAttempts,
                        TotalWins = wins,
                        TotalLosses = losses,
                        TotalDraws = draws,
                        TotalCleanSheets = g.Sum(p => p.Cleansheetsany),
                        TotalRedCards = g.Sum(p => p.Redcards),
                        TotalSaves = g.Sum(p => p.Saves),
                        TotalMom = g.Count(p => p.Mom),
                        TotalGoalsConceded = g.Sum(p => p.Goalsconceded),

                        AvgRating = g.Average(p => p.Rating),
                        PassAccuracyPercent = passAttempts > 0 ? passesMade * 100.0 / passAttempts : 0,
                        TackleSuccessPercent = tackleAttempts > 0 ? tacklesMade * 100.0 / tackleAttempts : 0,
                        GoalAccuracyPercent = shots > 0 ? goals * 100.0 / shots : 0,
                        WinPercent = matches > 0 ? wins * 100.0 / matches : 0,

                        // Para “uma partida”, isso reflete exatamente o estado; para agregados, indica se em ALGUMA partida ele desconectou.
                        Disconnected = g.Any(p => p.Disconnected)
                    };
                })
                .OrderByDescending(p => p.MatchesPlayed)
                .ToList();
        }


        public static List<ClubStatisticsDto> BuildPerClub(
    IEnumerable<MatchPlayerEntity> players,
    IReadOnlyDictionary<long, MatchClubEntity>? clubsById = null)
        {
            // Filtra jogadores desconectados
            var data = players.Where(p => p != null && !p.Disconnected);

            return data
                .GroupBy(p => p.ClubId)
                .Select(g =>
                {
                    MatchClubEntity? clubEntity = null;
                    if (clubsById != null && clubsById.TryGetValue(g.Key, out var mc))
                        clubEntity = mc;

                    // Agrupa por partida, mas apenas considerando entradas não-desconectadas
                    var matchesById = g.GroupBy(p => p.MatchId).ToList();
                    int matches = matchesById.Count;

                    // Para métricas por partida (wins/losses/goalsConceded), use um representante do grupo filtrado
                    int goalsConceded = matchesById.Sum(mg => (int)mg.First().Goalsconceded);
                    int wins = matchesById.Sum(mg => (int)mg.First().Wins);
                    int losses = matchesById.Sum(mg => (int)mg.First().Losses);
                    int draws = Math.Max(0, matches - wins - losses);

                    // Acúmulos por jogador (já filtrados)
                    int goals = g.Sum(p => p.Goals);
                    int shots = g.Sum(p => p.Shots);
                    int passesMade = g.Sum(p => p.Passesmade);
                    int passAttempts = g.Sum(p => p.Passattempts);
                    int tacklesMade = g.Sum(p => p.Tacklesmade);
                    int tackleAttempts = g.Sum(p => p.Tackleattempts);
                    int cleanSheets = g.Sum(p => p.Cleansheetsany);
                    int redCards = g.Sum(p => p.Redcards);
                    int saves = g.Sum(p => p.Saves);
                    int momCount = g.Count(p => p.Mom);
                    double avgRating = g.Any() ? g.Average(p => p.Rating) : 0.0;

                    return new ClubStatisticsDto
                    {
                        ClubId = g.Key,
                        ClubName = clubEntity?.Details?.Name ?? $"Clube {g.Key}",
                        ClubCrestAssetId = clubEntity?.Team.ToString(),

                        MatchesPlayed = matches,

                        TotalGoals = goals,
                        TotalGoalsConceded = goalsConceded,

                        TotalAssists = g.Sum(p => p.Assists),
                        TotalShots = shots,
                        TotalPassesMade = passesMade,
                        TotalPassAttempts = passAttempts,
                        TotalTacklesMade = tacklesMade,
                        TotalTackleAttempts = tackleAttempts,

                        TotalWins = wins,
                        TotalLosses = losses,
                        TotalDraws = draws,

                        TotalCleanSheets = cleanSheets,
                        TotalRedCards = redCards,
                        TotalSaves = saves,
                        TotalMom = momCount,

                        AvgRating = avgRating,

                        WinPercent = matches > 0 ? wins * 100.0 / matches : 0,
                        PassAccuracyPercent = passAttempts > 0 ? passesMade * 100.0 / passAttempts : 0,
                        TackleSuccessPercent = tackleAttempts > 0 ? tacklesMade * 100.0 / tackleAttempts : 0,
                        GoalAccuracyPercent = shots > 0 ? goals * 100.0 / shots : 0
                    };
                })
                // Se algum clube ficar sem jogadores após o filtro, ele nem entra no GroupBy/Select
                .OrderByDescending(c => c.MatchesPlayed)
                .ToList();
        }


        public static (MatchStatisticsDto Overall, List<PlayerStatisticsDto> Players, List<ClubStatisticsDto> Clubs)
    BuildLimitedForClub(long clubId, IReadOnlyList<MatchEntity> matches)
        {
            // Todos os jogadores do clube neste conjunto de partidas
            var allPlayers = matches.SelectMany(m => m.MatchPlayers)
                                    .Where(e => e.Player.ClubId == clubId)
                                    .ToList();

            // Apenas conectados (NOVO)
            var activePlayers = allPlayers.Where(p => !p.Disconnected).ToList();

            // Sides do clube (para W/L/D e gols contra por partida)
            var clubSides = matches.SelectMany(m => m.Clubs)
                                   .Where(c => c.ClubId == clubId)
                                   .ToList();

            // Se não houver nenhum jogador (ou todos desconectados), ainda retornamos stats do clube
            // com somatórios de jogador zerados.
            int matchesPlayedByClub = clubSides.Count;
            int winsCount = clubSides.Count(c => c.Goals > c.GoalsAgainst);
            int lossesCount = clubSides.Count(c => c.Goals < c.GoalsAgainst);
            int drawsCount = matchesPlayedByClub - winsCount - lossesCount;
            int cleanSheetsMatches = clubSides.Count(c => c.GoalsAgainst == 0);

            // Contar MOM apenas se o jogador do clube NÃO estava desconectado (NOVO)
            int momMatches = matches.Count(m =>
                m.MatchPlayers.Any(mp => mp.Player.ClubId == clubId && !mp.Disconnected && mp.Mom));

            // Stats por jogador (usa apenas conectados)
            var playersStats = BuildPerPlayer(activePlayers);

            // Somatórios só com conectados (NOVO)
            int totalShots = activePlayers.Sum(p => p.Shots);
            int totalPassesMade = activePlayers.Sum(p => p.Passesmade);
            int totalPassAttempts = activePlayers.Sum(p => p.Passattempts);
            int totalTacklesMade = activePlayers.Sum(p => p.Tacklesmade);
            int totalTackleAttempts = activePlayers.Sum(p => p.Tackleattempts);
            int totalGoals = activePlayers.Sum(p => p.Goals);
            int totalGoalsConceded = clubSides.Sum(c => c.GoalsAgainst); // nível de clube, mantém

            int distinctPlayersCount = playersStats.Count;

            var firstSide = clubSides.FirstOrDefault();
            string clubName = firstSide?.Details?.Name ?? $"Clube {clubId}";
            string? crestAssetId = firstSide?.Details?.CrestAssetId;

            var clubsStats = new List<ClubStatisticsDto>
    {
        new()
        {
            ClubId = (int)clubId,
            ClubName = clubName,
            ClubCrestAssetId = crestAssetId,
            MatchesPlayed = matchesPlayedByClub,

            TotalGoals = totalGoals,
            TotalGoalsConceded = totalGoalsConceded,

            TotalAssists = activePlayers.Sum(p => p.Assists),
            TotalShots = totalShots,
            TotalPassesMade = totalPassesMade,
            TotalPassAttempts = totalPassAttempts,
            TotalTacklesMade = totalTacklesMade,
            TotalTackleAttempts = totalTackleAttempts,

            TotalWins = winsCount,
            TotalLosses = lossesCount,
            TotalDraws = drawsCount,

            TotalCleanSheets = cleanSheetsMatches,
            TotalRedCards = activePlayers.Sum(p => p.Redcards),
            TotalSaves = activePlayers.Sum(p => p.Saves),
            TotalMom = momMatches,

            AvgRating = activePlayers.Any() ? activePlayers.Average(p => p.Rating) : 0,

            WinPercent = matchesPlayedByClub > 0 ? winsCount * 100.0 / matchesPlayedByClub : 0,
            PassAccuracyPercent = totalPassAttempts > 0 ? totalPassesMade * 100.0 / totalPassAttempts : 0,
            TackleSuccessPercent = totalTackleAttempts > 0 ? totalTacklesMade * 100.0 / totalTackleAttempts : 0,
            GoalAccuracyPercent = totalShots > 0 ? totalGoals * 100.0 / totalShots : 0
        }
    };

            var overall = new MatchStatisticsDto
            {
                TotalMatches = matchesPlayedByClub,
                TotalPlayers = distinctPlayersCount,

                TotalGoals = playersStats.Sum(p => p.TotalGoals),
                TotalAssists = playersStats.Sum(p => p.TotalAssists),
                TotalShots = totalShots,
                TotalPassesMade = playersStats.Sum(p => p.TotalPassesMade),
                TotalPassAttempts = totalPassAttempts,
                TotalTacklesMade = playersStats.Sum(p => p.TotalTacklesMade),
                TotalTackleAttempts = totalTackleAttempts,

                TotalRating = activePlayers.Sum(p => p.Rating),

                TotalWins = winsCount,
                TotalLosses = lossesCount,
                TotalDraws = drawsCount,
                TotalCleanSheets = cleanSheetsMatches,
                TotalRedCards = playersStats.Sum(p => p.TotalRedCards),
                TotalSaves = playersStats.Sum(p => p.TotalSaves),
                TotalMom = momMatches,

                WinPercent = matchesPlayedByClub > 0 ? winsCount * 100.0 / matchesPlayedByClub : 0,
                LossPercent = matchesPlayedByClub > 0 ? lossesCount * 100.0 / matchesPlayedByClub : 0,
                DrawPercent = matchesPlayedByClub > 0 ? drawsCount * 100.0 / matchesPlayedByClub : 0,
                CleanSheetsPercent = matchesPlayedByClub > 0 ? cleanSheetsMatches * 100.0 / matchesPlayedByClub : 0,
                MomPercent = matchesPlayedByClub > 0 ? momMatches * 100.0 / matchesPlayedByClub : 0,

                PassAccuracyPercent = totalPassAttempts > 0
                    ? playersStats.Sum(p => p.TotalPassesMade) * 100.0 / totalPassAttempts : 0,
                TackleSuccessPercent = totalTackleAttempts > 0
                    ? playersStats.Sum(p => p.TotalTacklesMade) * 100.0 / totalTackleAttempts : 0,
                GoalAccuracyPercent = totalShots > 0
                    ? playersStats.Sum(p => p.TotalGoals) * 100.0 / totalShots : 0
            };

            return (overall, playersStats, clubsStats);
        }


        public static List<ClubOverallStatsDto> BuildClubsOverall(IEnumerable<OverallStatsEntity> entities)
        {
            return entities.Select(o => new ClubOverallStatsDto
            {
                ClubId = o.ClubId,
                BestDivision = o.BestDivision,
                BestFinishGroup = o.BestFinishGroup,
                GamesPlayed = o.GamesPlayed,
                GamesPlayedPlayoff = o.GamesPlayedPlayoff,
                Goals = o.Goals,
                GoalsAgainst = o.GoalsAgainst,
                Promotions = o.Promotions,
                Relegations = o.Relegations,
                Losses = o.Losses,
                Ties = o.Ties,
                Wins = o.Wins,
                Wstreak = o.Wstreak,
                Unbeatenstreak = o.Unbeatenstreak,
                SkillRating = o.SkillRating,
                Reputationtier = o.Reputationtier,
                LeagueAppearances = o.LeagueAppearances,
                CurrentDivision = o.CurrentDivision.ToString(),
                UpdatedAtUtc = o.UpdatedAtUtc
            })
            .OrderBy(x => x.ClubId)
            .ToList();
        }

        private static int TryParseSeasonAsNumber(string? seasonId)
        {
            if (string.IsNullOrWhiteSpace(seasonId)) return int.MinValue;
            return int.TryParse(seasonId, out var n) ? n : int.MinValue;
        }

        public static List<ClubPlayoffAchievementDto> BuildClubsPlayoffAchievements(
            IEnumerable<PlayoffAchievementEntity> entities)
        {
            return entities
                .GroupBy(e => e.ClubId)
                .Select(g => new ClubPlayoffAchievementDto
                {
                    ClubId = g.Key,
                    Achievements = g
                        .OrderByDescending(e => TryParseSeasonAsNumber(e.SeasonId))
                        .ThenByDescending(e => e.UpdatedAtUtc)
                        .Select(e => new PlayoffAchievementDto
                        {
                            SeasonId = e.SeasonId,
                            SeasonName = e.SeasonName,
                            BestDivision = e.BestDivision,
                            BestFinishGroup = e.BestFinishGroup,
                            RetrievedAtUtc = e.RetrievedAtUtc,
                            UpdatedAtUtc = e.UpdatedAtUtc
                        })
                        .ToList()
                })
                .OrderBy(x => x.ClubId)
                .ToList();
        }

        /// <summary>
        /// Agrupa por Player.PlayerId (global) para que o mesmo jogador, jogando por clubes diferentes,
        /// apareça apenas uma vez no modo "clubes agrupados".
        /// </summary>
        public static List<PlayerStatisticsDto> BuildPerPlayerMergedByGlobalId(IEnumerable<MatchPlayerEntity> players)
        {
            // Garantir materialização para não iterar várias vezes
            var list = players as IList<MatchPlayerEntity> ?? players.ToList();

            return list
                .Where(p => p.Player != null) // precisamos do Player.PlayerId
                .GroupBy(p => p.Player.PlayerId) // <-- chave global do jogador
                .Select(g =>
                {
                    // Pega um "representante" — o mais recente por MatchId (maior MatchId) para nome/club fallback
                    var repr = g.OrderByDescending(x => x.MatchId).First();
                    var matchRepr = repr.Match;

                    int matches = g.Count();
                    int goals = g.Sum(p => p.Goals);
                    int shots = g.Sum(p => p.Shots);
                    int passesMade = g.Sum(p => p.Passesmade);
                    int passAttempts = g.Sum(p => p.Passattempts);
                    int tacklesMade = g.Sum(p => p.Tacklesmade);
                    int tackleAttempts = g.Sum(p => p.Tackleattempts);
                    int wins = g.Sum(p => p.Wins);
                    int losses = g.Sum(p => p.Losses);
                    int draws = Math.Max(0, matches - wins - losses);

                    return new PlayerStatisticsDto
                    {
                        // Usa o PlayerId global na saída (se o seu DTO já é long, ajuste o tipo)
                        PlayerId = repr.Player.PlayerId,
                        PlayerName = repr.Player.Playername ?? "Unknown",
                        // Como estamos agrupando clubes, não faz sentido devolver um único ClubId real.
                        // Use 0 (ou null se seu DTO permitir) apenas para preencher o contrato atual.
                        ClubId = 0,
                        Date = matchRepr.Timestamp,

                        ProName = repr.ProName,
                        ProOverallStr = repr.ProOverallStr,
                        ProHeight = repr.ProHeight,

                        MatchesPlayed = matches,
                        TotalGoals = goals,
                        TotalAssists = g.Sum(p => p.Assists),
                        TotalShots = shots,
                        TotalPassesMade = passesMade,
                        TotalPassAttempts = passAttempts,
                        TotalTacklesMade = tacklesMade,
                        TotalTackleAttempts = tackleAttempts,
                        TotalWins = wins,
                        TotalLosses = losses,
                        TotalDraws = draws,
                        TotalCleanSheets = g.Sum(p => p.Cleansheetsany),
                        TotalRedCards = g.Sum(p => p.Redcards),
                        TotalSaves = g.Sum(p => p.Saves),
                        TotalMom = g.Count(p => p.Mom),
                        TotalGoalsConceded = g.Sum(p => p.Goalsconceded),

                        AvgRating = g.Any() ? g.Average(p => p.Rating) : 0,
                        PassAccuracyPercent = passAttempts > 0 ? (passesMade * 100.0) / passAttempts : 0,
                        TackleSuccessPercent = tackleAttempts > 0 ? (tacklesMade * 100.0) / tackleAttempts : 0,
                        GoalAccuracyPercent = shots > 0 ? (goals * 100.0) / shots : 0,
                        WinPercent = matches > 0 ? (wins * 100.0) / matches : 0
                    };
                })
                // opcional: ordenar por partidas (ou por gols, etc.)
                .OrderByDescending(p => p.MatchesPlayed)
                .ToList();
        }

        /// <summary>
        /// Produz um único ClubStatisticsDto agregando todos os MatchPlayers recebidos
        /// (modo "clubes agrupados como um só").
        /// </summary>
        public static ClubStatisticsDto BuildSingleClubFromPlayers(IEnumerable<MatchPlayerEntity> players, string? clubName = null, string? crestAssetId = null)
        {
            var list = players as IList<MatchPlayerEntity> ?? players.ToList();

            // Agregação por partida para wins/losses/draws sem multiplicar por número de jogadores
            var matchesById = list.GroupBy(p => p.MatchId).ToList();
            int matches = matchesById.Count;

            int wins = matchesById.Sum(mg => (int)mg.First().Wins);
            int losses = matchesById.Sum(mg => (int)mg.First().Losses);
            int draws = Math.Max(0, matches - wins - losses);

            int goalsConceded = matchesById.Sum(mg => (int)mg.First().Goalsconceded);

            int goals = list.Sum(p => p.Goals);
            int shots = list.Sum(p => p.Shots);
            int passesMade = list.Sum(p => p.Passesmade);
            int passAttempts = list.Sum(p => p.Passattempts);
            int tacklesMade = list.Sum(p => p.Tacklesmade);
            int tackleAttempts = list.Sum(p => p.Tackleattempts);
            int cleanSheets = list.Sum(p => p.Cleansheetsany);
            int redCards = list.Sum(p => p.Redcards);
            int saves = list.Sum(p => p.Saves);
            int momCount = list.Count(p => p.Mom);
            double avgRating = list.Any() ? list.Average(p => p.Rating) : 0.0;

            return new ClubStatisticsDto
            {
                ClubId = 0, // marcador "agregado"
                ClubName = string.IsNullOrWhiteSpace(clubName) ? "Clubes agrupados" : clubName,
                ClubCrestAssetId = crestAssetId,

                MatchesPlayed = matches,

                TotalGoals = goals,
                TotalGoalsConceded = goalsConceded,

                TotalAssists = list.Sum(p => p.Assists),
                TotalShots = shots,
                TotalPassesMade = passesMade,
                TotalPassAttempts = passAttempts,
                TotalTacklesMade = tacklesMade,
                TotalTackleAttempts = tackleAttempts,

                TotalWins = wins,
                TotalLosses = losses,
                TotalDraws = draws,

                TotalCleanSheets = cleanSheets,
                TotalRedCards = redCards,
                TotalSaves = saves,
                TotalMom = momCount,

                AvgRating = avgRating,

                WinPercent = matches > 0 ? wins * 100.0 / matches : 0,
                PassAccuracyPercent = passAttempts > 0 ? passesMade * 100.0 / passAttempts : 0,
                TackleSuccessPercent = tackleAttempts > 0 ? tacklesMade * 100.0 / tackleAttempts : 0,
                GoalAccuracyPercent = shots > 0 ? goals * 100.0 / shots : 0
            };
        }
    }
}