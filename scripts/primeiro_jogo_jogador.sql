-- Primeiro jogo de um jogador
-- Substitua 'NomeDoJogador' pelo nome exato (ou parte do nome com ILIKE)

SELECT
    COALESCE(mp."ProName", p."Playername")            AS jogador,
    p."Playername"                                     AS nome_conta,
    p."PlayerId"                                       AS player_id,
    p."ClubId"                                         AS clube_id,
    MIN(m."Timestamp")                                 AS primeiro_jogo,
    COUNT(DISTINCT mp."MatchId")                       AS total_partidas
FROM "MatchPlayers" mp
JOIN "Players"      p ON p."Id" = mp."PlayerEntityId"
JOIN "Matches"      m ON m."MatchId" = mp."MatchId"
WHERE
    -- Filtra por nome do pro (campo livre) OU nome da conta EA
    COALESCE(mp."ProName", p."Playername") ILIKE '%NomeDoJogador%'
GROUP BY
    COALESCE(mp."ProName", p."Playername"),
    p."Playername",
    p."PlayerId",
    p."ClubId"
ORDER BY primeiro_jogo;
