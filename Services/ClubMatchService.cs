﻿using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;

public class ClubMatchService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly EAFCContext _db;

    public ClubMatchService(HttpClient httpClient, IConfiguration config, EAFCContext db)
    {
        _httpClient = httpClient;
        _config = config;
        _db = db;
    }

    public async Task FetchAndStoreMatchesAsync(string clubId, string matchType)
    {
        List<Match> matches = await FetchMatches(clubId, matchType);

        foreach (Match match in matches)
        {
            await SaveMatchAsync(match, matchType);
        }
    }

    private async Task<List<Match>> FetchMatches(string clubId, string matchType)
    {
        var endpointTemplate = _config["EAFCSettings:ClubMatchesEndpoint"];
        var endpoint = new Uri(_config["EAFCSettings:BaseUrl"]) + string.Format(endpointTemplate, clubId, matchType);

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PostmanRuntime/7.36.0");
        _httpClient.DefaultRequestHeaders.Connection.ParseAdd("keep-alive");

        try
        {
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            List<Match>? matches = JsonSerializer.Deserialize<List<Match>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new Exception();

            return matches;

        }
        catch (Exception ex)
        {

            throw;
        }
        return null;
    }

    public async Task SaveMatchAsync(Match match, string matchType)
    {
        if (match == null || string.IsNullOrEmpty(match.MatchId))
            throw new ArgumentException("Match inválido.");

        long matchId = Convert.ToInt64(match.MatchId);

        if (await _db.Matches.AnyAsync(m => m.MatchId == matchId))
            return;

        var matchEntity = new MatchEntity
        {
            MatchId = matchId,
            MatchType = matchType == "leagueMatch" ? MatchType.League : MatchType.Playoff,
            Timestamp = DateTimeOffset.FromUnixTimeSeconds(match.Timestamp).UtcDateTime,
        };

        await _db.Matches.AddAsync(matchEntity);

        // === CLUBES ===
        var clubIds = match.Clubs?.Keys.Select(long.Parse).ToList() ?? new List<long>();

        foreach (var entry in match.Clubs)
        {
            long clubId = long.Parse(entry.Key);
            var club = entry.Value;

            var details = new ClubDetailsEntity
            {
                ClubId = clubId,
                Name = club.Details?.Name,
                RegionId = club.Details?.RegionId ?? 0,
                TeamId = club.Details?.TeamId ?? 0,
                StadName = club.Details?.CustomKit?.StadName,
                KitId = club.Details?.CustomKit?.KitId,
                CustomKitId = club.Details?.CustomKit?.CustomKitId,
                CustomAwayKitId = club.Details?.CustomKit?.CustomAwayKitId,
                CustomThirdKitId = club.Details?.CustomKit?.CustomThirdKitId,
                CustomKeeperKitId = club.Details?.CustomKit?.CustomKeeperKitId,
                KitColor1 = club.Details?.CustomKit?.KitColor1,
                KitColor2 = club.Details?.CustomKit?.KitColor2,
                KitColor3 = club.Details?.CustomKit?.KitColor3,
                KitColor4 = club.Details?.CustomKit?.KitColor4,
                KitAColor1 = club.Details?.CustomKit?.KitAColor1,
                KitAColor2 = club.Details?.CustomKit?.KitAColor2,
                KitAColor3 = club.Details?.CustomKit?.KitAColor3,
                KitAColor4 = club.Details?.CustomKit?.KitAColor4,
                KitThrdColor1 = club.Details?.CustomKit?.KitThrdColor1,
                KitThrdColor2 = club.Details?.CustomKit?.KitThrdColor2,
                KitThrdColor3 = club.Details?.CustomKit?.KitThrdColor3,
                KitThrdColor4 = club.Details?.CustomKit?.KitThrdColor4,
                DCustomKit = club.Details?.CustomKit?.DCustomKit,
                CrestColor = club.Details?.CustomKit?.CrestColor,
                CrestAssetId = club.Details?.CustomKit?.CrestAssetId
            };

            var matchClub = new MatchClubEntity
            {
                MatchId = matchId,
                ClubId = clubId,
                Date = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(club.Date)).UtcDateTime,
                GameNumber = Convert.ToInt32(club.GameNumber),
                Goals = Convert.ToInt16(club.Goals),
                GoalsAgainst = Convert.ToInt16(club.GoalsAgainst),
                Losses = Convert.ToInt16(club.Losses),
                MatchType = Convert.ToInt16(club.MatchType),
                Result = Convert.ToInt16(club.Result),
                Score = Convert.ToInt16(club.Score),
                SeasonId = Convert.ToInt16(club.SeasonId),
                Team = Convert.ToInt32(club.TEAM),
                Ties = Convert.ToInt16(club.Ties),
                Wins = Convert.ToInt16(club.Wins),
                WinnerByDnf = club.WinnerByDnf == "1",
                Details = details
            };

            await _db.MatchClubs.AddAsync(matchClub);
        }

        // === JOGADORES ===
        var matchPlayerEntities = new List<MatchPlayerEntity>();

        if (match.Players != null)
        {
            var playerKeys = match.Players
                .SelectMany(club => club.Value.Select(player => (PlayerId: long.Parse(player.Key), ClubId: long.Parse(club.Key))))
                .ToList();

            var existingPlayers = await _db.Players
                .Where(p => playerKeys.Select(k => k.PlayerId).Contains(p.PlayerId))
                .ToDictionaryAsync(p => (p.PlayerId, p.ClubId));

            foreach (var clubEntry in match.Players)
            {
                long clubId = long.Parse(clubEntry.Key);

                foreach (var playerEntry in clubEntry.Value)
                {
                    long playerId = long.Parse(playerEntry.Key);
                    var data = playerEntry.Value;
                    var key = (playerId, clubId);

                    if (!existingPlayers.TryGetValue(key, out var playerEntity))
                    {
                        playerEntity = new PlayerEntity
                        {
                            PlayerId = playerId,
                            ClubId = clubId,
                            Playername = data.Playername
                        };
                        await _db.Players.AddAsync(playerEntity);
                        await _db.SaveChangesAsync();
                        existingPlayers.Add(key, playerEntity);
                    }
                    else if (playerEntity.Playername != data.Playername)
                    {
                        playerEntity.Playername = data.Playername;
                        _db.Players.Update(playerEntity);
                    }

                    var parsedStats = PlayerMatchStatsEntity.Parse(data.Vproattr);

                    var lastStats = await _db.PlayerMatchStats
                        .Where(s => s.PlayerEntityId == playerEntity.Id)
                        .OrderByDescending(s => s.Id)
                        .FirstOrDefaultAsync();

                    long statsId;
                    if (lastStats == null || !parsedStats.IsEqualTo(lastStats))
                    {
                        parsedStats.PlayerEntityId = playerEntity.Id;
                        await _db.PlayerMatchStats.AddAsync(parsedStats);
                        await _db.SaveChangesAsync();
                        statsId = parsedStats.Id;
                        playerEntity.PlayerMatchStatsId = parsedStats.Id;
                        _db.Players.Update(playerEntity);
                    }
                    else
                    {
                        statsId = lastStats.Id;
                    }

                    matchPlayerEntities.Add(new MatchPlayerEntity
                    {
                        MatchId = matchId,
                        ClubId = clubId,
                        PlayerEntityId = playerEntity.Id,
                        PlayerMatchStatsEntityId = statsId,
                        Assists = Convert.ToInt16(data.Assists),
                        Cleansheetsany = Convert.ToInt16(data.Cleansheetsany),
                        Cleansheetsdef = Convert.ToInt16(data.Cleansheetsdef),
                        Cleansheetsgk = Convert.ToInt16(data.Cleansheetsgk),
                        Goals = Convert.ToInt16(data.Goals),
                        Goalsconceded = Convert.ToInt16(data.Goalsconceded),
                        Losses = Convert.ToInt16(data.Losses),
                        Mom = data.Mom == "1",
                        Namespace = Convert.ToInt16(data.Namespace),
                        Passattempts = Convert.ToInt16(data.Passattempts),
                        Passesmade = Convert.ToInt16(data.Passesmade),
                        Pos = data.Pos,
                        Rating = Convert.ToDouble(data.Rating),
                        Realtimegame = data.Realtimegame,
                        Realtimeidle = data.Realtimeidle,
                        Redcards = Convert.ToInt16(data.Redcards),
                        Saves = Convert.ToInt16(data.Saves),
                        Score = Convert.ToInt16(data.Score),
                        Shots = Convert.ToInt16(data.Shots),
                        Tackleattempts = Convert.ToInt16(data.Tackleattempts),
                        Tacklesmade = Convert.ToInt16(data.Tacklesmade),
                        Vproattr = data.Vproattr,
                        Vprohackreason = data.Vprohackreason,
                        Wins = Convert.ToInt16(data.Wins)
                    });
                }
            }
        }

        await _db.MatchPlayers.AddRangeAsync(matchPlayerEntities);
        await _db.SaveChangesAsync();
    }


    //private async Task<List<MatchClubEntity>> MapClubsAsync(Match match)
    //{
    //    var result = new List<MatchClubEntity>();
    //    if (match.Clubs == null) return result;

    //    long matchId = Convert.ToInt64(match.MatchId);

    //    var clubIds = match.Clubs.Keys
    //        .Where(k => long.TryParse(k, out _))
    //        .Select(k => long.Parse(k))
    //        .ToList();

    //    // Carrega os ClubDetails existentes no banco
    //    var existingDetails = await _db.ClubDetails
    //        .Where(cd => clubIds.Contains(cd.ClubId))
    //        .ToDictionaryAsync(cd => cd.ClubId);

    //    foreach (var entry in match.Clubs)
    //    {
    //        if (!long.TryParse(entry.Key, out var clubId)) continue;
    //        var club = entry.Value;

    //        ClubDetailsEntity clubDetails;
    //        if (existingDetails.TryGetValue(clubId, out var existingDetail))
    //        {
    //            // Atualiza os campos da entidade existente
    //            existingDetail.Name = club.Details?.Name;
    //            existingDetail.RegionId = club.Details?.RegionId ?? 0;
    //            existingDetail.TeamId = club.Details?.TeamId ?? 0;
    //            existingDetail.StadName = club.Details?.CustomKit?.StadName;
    //            existingDetail.KitId = club.Details?.CustomKit?.KitId;
    //            existingDetail.CustomKitId = club.Details?.CustomKit?.CustomKitId;
    //            existingDetail.CustomAwayKitId = club.Details?.CustomKit?.CustomAwayKitId;
    //            existingDetail.CustomThirdKitId = club.Details?.CustomKit?.CustomThirdKitId;
    //            existingDetail.CustomKeeperKitId = club.Details?.CustomKit?.CustomKeeperKitId;
    //            existingDetail.KitColor1 = club.Details?.CustomKit?.KitColor1;
    //            existingDetail.KitColor2 = club.Details?.CustomKit?.KitColor2;
    //            existingDetail.KitColor3 = club.Details?.CustomKit?.KitColor3;
    //            existingDetail.KitColor4 = club.Details?.CustomKit?.KitColor4;
    //            existingDetail.KitAColor1 = club.Details?.CustomKit?.KitAColor1;
    //            existingDetail.KitAColor2 = club.Details?.CustomKit?.KitAColor2;
    //            existingDetail.KitAColor3 = club.Details?.CustomKit?.KitAColor3;
    //            existingDetail.KitAColor4 = club.Details?.CustomKit?.KitAColor4;
    //            existingDetail.KitThrdColor1 = club.Details?.CustomKit?.KitThrdColor1;
    //            existingDetail.KitThrdColor2 = club.Details?.CustomKit?.KitThrdColor2;
    //            existingDetail.KitThrdColor3 = club.Details?.CustomKit?.KitThrdColor3;
    //            existingDetail.KitThrdColor4 = club.Details?.CustomKit?.KitThrdColor4;
    //            existingDetail.DCustomKit = club.Details?.CustomKit?.DCustomKit;
    //            existingDetail.CrestColor = club.Details?.CustomKit?.CrestColor;
    //            existingDetail.CrestAssetId = club.Details?.CustomKit?.CrestAssetId;

    //            _db.ClubDetails.Update(existingDetail);
    //            clubDetails = existingDetail;
    //        }
    //        else
    //        {
    //            // Cria nova entidade se não existir
    //            clubDetails = new ClubDetailsEntity
    //            {
    //                ClubId = clubId,
    //                Name = club.Details?.Name,
    //                RegionId = club.Details?.RegionId ?? 0,
    //                TeamId = club.Details?.TeamId ?? 0,
    //                StadName = club.Details?.CustomKit?.StadName,
    //                KitId = club.Details?.CustomKit?.KitId,
    //                CustomKitId = club.Details?.CustomKit?.CustomKitId,
    //                CustomAwayKitId = club.Details?.CustomKit?.CustomAwayKitId,
    //                CustomThirdKitId = club.Details?.CustomKit?.CustomThirdKitId,
    //                CustomKeeperKitId = club.Details?.CustomKit?.CustomKeeperKitId,
    //                KitColor1 = club.Details?.CustomKit?.KitColor1,
    //                KitColor2 = club.Details?.CustomKit?.KitColor2,
    //                KitColor3 = club.Details?.CustomKit?.KitColor3,
    //                KitColor4 = club.Details?.CustomKit?.KitColor4,
    //                KitAColor1 = club.Details?.CustomKit?.KitAColor1,
    //                KitAColor2 = club.Details?.CustomKit?.KitAColor2,
    //                KitAColor3 = club.Details?.CustomKit?.KitAColor3,
    //                KitAColor4 = club.Details?.CustomKit?.KitAColor4,
    //                KitThrdColor1 = club.Details?.CustomKit?.KitThrdColor1,
    //                KitThrdColor2 = club.Details?.CustomKit?.KitThrdColor2,
    //                KitThrdColor3 = club.Details?.CustomKit?.KitThrdColor3,
    //                KitThrdColor4 = club.Details?.CustomKit?.KitThrdColor4,
    //                DCustomKit = club.Details?.CustomKit?.DCustomKit,
    //                CrestColor = club.Details?.CustomKit?.CrestColor,
    //                CrestAssetId = club.Details?.CustomKit?.CrestAssetId
    //            };

    //            await _db.ClubDetails.AddAsync(clubDetails);
    //        }

    //        var clubEntity = new MatchClubEntity
    //        {
    //            MatchId = matchId,
    //            ClubId = clubId,
    //            Date = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(club.Date)).UtcDateTime,
    //            GameNumber = Convert.ToInt32(club.GameNumber),
    //            Goals = Convert.ToInt16(club.Goals),
    //            GoalsAgainst = Convert.ToInt16(club.GoalsAgainst),
    //            Losses = Convert.ToInt16(club.Losses),
    //            MatchType = Convert.ToInt16(club.MatchType),
    //            Result = Convert.ToInt16(club.Result),
    //            Score = Convert.ToInt16(club.Score),
    //            SeasonId = Convert.ToInt16(club.SeasonId),
    //            Team = Convert.ToInt32(club.TEAM),
    //            Ties = Convert.ToInt16(club.Ties),
    //            WinnerByDnf = club.WinnerByDnf == "1",
    //            Wins = Convert.ToInt16(club.Wins),
    //            Details = clubDetails
    //        };

    //        result.Add(clubEntity);
    //    }

    //    return result;
    //}

    //private async Task<List<MatchPlayerEntity>> MapPlayersAsync(Match match)
    //{
    //    var result = new List<MatchPlayerEntity>();
    //    if (match.Players == null) return result;

    //    long matchId = Convert.ToInt64(match.MatchId);

    //    var playerKeys = match.Players
    //        .SelectMany(c => c.Value, (club, player) => (
    //            PlayerId: long.Parse(player.Key),
    //            ClubId: long.Parse(club.Key)))
    //        .ToList();

    //    var existingPlayers = await _db.Players
    //        .Where(p => playerKeys.Select(k => k.PlayerId).Contains(p.PlayerId))
    //        .ToDictionaryAsync(p => (p.PlayerId, p.ClubId));

    //    foreach (var clubEntry in match.Players)
    //    {
    //        if (!long.TryParse(clubEntry.Key, out var clubId)) continue;

    //        foreach (var playerEntry in clubEntry.Value)
    //        {
    //            if (!long.TryParse(playerEntry.Key, out var playerId)) continue;

    //            var playerData = playerEntry.Value;
    //            var key = (playerId, clubId);

    //            PlayerEntity playerEntity;

    //            if (existingPlayers.TryGetValue(key, out var existing))
    //            {
    //                // Atualiza nome se tiver mudado
    //                if (existing.Playername != playerData.Playername)
    //                {
    //                    existing.Playername = playerData.Playername;
    //                    _db.Players.Update(existing);
    //                }

    //                playerEntity = existing;
    //            }
    //            else
    //            {
    //                playerEntity = new PlayerEntity
    //                {
    //                    ClubId = clubId,
    //                    PlayerId = playerId,
    //                    Playername = playerData.Playername
    //                };

    //                await _db.Players.AddAsync(playerEntity);
    //                await _db.SaveChangesAsync(); // necessário para gerar o ID

    //                existingPlayers.Add(key, playerEntity);
    //            }

    //            var playerMatch = new MatchPlayerEntity
    //            {
    //                MatchId = matchId,
    //                ClubId = clubId,
    //                PlayerEntityId = playerEntity.Id,
    //                Assists = Convert.ToInt16(playerData.Assists),
    //                Cleansheetsany = Convert.ToInt16(playerData.Cleansheetsany),
    //                Cleansheetsdef = Convert.ToInt16(playerData.Cleansheetsdef),
    //                Cleansheetsgk = Convert.ToInt16(playerData.Cleansheetsgk),
    //                Goals = Convert.ToInt16(playerData.Goals),
    //                Goalsconceded = Convert.ToInt16(playerData.Goalsconceded),
    //                Losses = Convert.ToInt16(playerData.Losses),
    //                Mom = playerData.Mom == "1",
    //                Namespace = Convert.ToInt16(playerData.Namespace),
    //                Passattempts = Convert.ToInt16(playerData.Passattempts),
    //                Passesmade = Convert.ToInt16(playerData.Passesmade),
    //                Pos = playerData.Pos,
    //                Rating = Convert.ToDouble(playerData.Rating),
    //                Realtimegame = playerData.Realtimegame,
    //                Realtimeidle = playerData.Realtimeidle,
    //                Redcards = Convert.ToInt16(playerData.Redcards),
    //                Saves = Convert.ToInt16(playerData.Saves),
    //                Score = Convert.ToInt16(playerData.Score),
    //                Shots = Convert.ToInt16(playerData.Shots),
    //                Tackleattempts = Convert.ToInt16(playerData.Tackleattempts),
    //                Tacklesmade = Convert.ToInt16(playerData.Tacklesmade),
    //                Vproattr = playerData.Vproattr,
    //                Vprohackreason = playerData.Vprohackreason,
    //                Wins = Convert.ToInt16(playerData.Wins),
    //            };

    //            // Gerenciar estatísticas (PlayerMatchStats)
    //            var parsedStats = PlayerMatchStatsEntity.Parse(playerData.Vproattr);

    //            var lastStats = await _db.PlayerMatchStats
    //                .Where(s => s.PlayerEntityId == playerEntity.Id)
    //                .OrderByDescending(s => s.Id)
    //                .FirstOrDefaultAsync();

    //            if (lastStats == null || !parsedStats.IsEqualTo(lastStats))
    //            {
    //                parsedStats.PlayerEntityId = playerEntity.Id;
    //                await _db.PlayerMatchStats.AddAsync(parsedStats);
    //                await _db.SaveChangesAsync();

    //                playerEntity.PlayerMatchStatsId = parsedStats.Id;
    //                _db.Players.Update(playerEntity);

    //                playerMatch.PlayerMatchStatsEntityId = parsedStats.Id;
    //            }
    //            else
    //            {
    //                playerMatch.PlayerMatchStatsEntityId = lastStats.Id;
    //            }

    //            result.Add(playerMatch);
    //        }
    //    }

    //    return result;
    //}

}
