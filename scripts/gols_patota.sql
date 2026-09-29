-- ==========================================================
-- GOLS DA PATOTA
-- ==========================================================

-- 1. Total de gols
SELECT COUNT(*) AS total_gols
FROM "MatchGoalLinks";

-- ==========================================================

-- 2. Ranking de gols por jogador
SELECT
    COALESCE(mp."ProName", p."Playername") AS jogador,
    COUNT(*)                               AS gols
FROM "MatchGoalLinks" g
JOIN "Players"      p  ON p."Id"           = g."ScorerPlayerEntityId"
JOIN "MatchPlayers" mp ON mp."MatchId"     = g."MatchId"
                      AND mp."ClubId"      = g."ClubId"
                      AND mp."PlayerEntityId" = g."ScorerPlayerEntityId"
GROUP BY COALESCE(mp."ProName", p."Playername")
ORDER BY gols DESC;

-- ==========================================================

-- 3. Gols marco (100, 200, 300, ...)
WITH gols_numerados AS (
    SELECT
        ROW_NUMBER() OVER (ORDER BY m."Timestamp", g."Id") AS num_gol,
        COALESCE(mp."ProName", p."Playername")             AS jogador,
        m."Timestamp"                                       AS data_partida,
        g."MatchId"
    FROM "MatchGoalLinks" g
    JOIN "Players"      p  ON p."Id"           = g."ScorerPlayerEntityId"
    JOIN "Matches"      m  ON m."MatchId"       = g."MatchId"
    JOIN "MatchPlayers" mp ON mp."MatchId"      = g."MatchId"
                          AND mp."ClubId"       = g."ClubId"
                          AND mp."PlayerEntityId" = g."ScorerPlayerEntityId"
)
SELECT
    num_gol    AS marco,
    jogador,
    data_partida,
    "MatchId"
FROM gols_numerados
WHERE num_gol % 100 = 0
ORDER BY num_gol;
