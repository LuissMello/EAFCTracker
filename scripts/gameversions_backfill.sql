-- Backfill de GameVersionId (mesma lógica da migration 20260928221334_GameVersionsEIndices).
-- Idempotente: só toca linhas com "GameVersionId" IS NULL. Rode manualmente APENAS se a migration já tiver sido
-- aplicada sem o backfill (ex.: instância iniciada durante a implementação) e as colunas estiverem NULL.
--
-- Premissas: clubes rastreados hoje = FC26 (nenhum clube do FC27 rastreado ainda); partidas/overall existentes são anteriores ao FC27;
-- corte 2025-09-26 00:00 UTC: antes -> FC25, depois -> FC26. PlayoffAchievements ficam NULL.

BEGIN;

UPDATE "TrackedClubs"
SET "GameVersionId" = (SELECT gv."Id" FROM "GameVersions" AS gv WHERE gv."Version" = 26)
WHERE "GameVersionId" IS NULL;

UPDATE "Matches"
SET "GameVersionId" = CASE
        WHEN "Timestamp" < TIMESTAMPTZ '2025-09-26 00:00:00+00'
            THEN (SELECT gv."Id" FROM "GameVersions" AS gv WHERE gv."Version" = 25)
        ELSE (SELECT gv."Id" FROM "GameVersions" AS gv WHERE gv."Version" = 26)
    END
WHERE "GameVersionId" IS NULL;

UPDATE "OverallStats" AS o
SET "GameVersionId" = CASE
        WHEN COALESCE(
                (SELECT m."Timestamp" FROM "Matches" AS m WHERE m."MatchId" = o."MatchId"),
                o."UpdatedAtUtc") < TIMESTAMPTZ '2025-09-26 00:00:00+00'
            THEN (SELECT gv."Id" FROM "GameVersions" AS gv WHERE gv."Version" = 25)
        ELSE (SELECT gv."Id" FROM "GameVersions" AS gv WHERE gv."Version" = 26)
    END
WHERE o."GameVersionId" IS NULL;

COMMIT;
