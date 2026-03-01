using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EAFCMatchTracker.Migrations
{
    /// <inheritdoc />
    public partial class CorrecaoMatchClubs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 0) cria a sequência se não existir (mesmo nome/case)
            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_class c
    JOIN pg_namespace n ON n.oid = c.relnamespace
    WHERE c.relkind = 'S'
      AND n.nspname = 'public'
      AND c.relname = 'MatchClubs_Id_seq'
  ) THEN
    CREATE SEQUENCE ""public"".""MatchClubs_Id_seq"";
  END IF;
END $$;");

            // 1) normaliza Ids (usa a sequência com IDENTIFICADORES ENTRE ASPAS)
            migrationBuilder.Sql(@"
WITH tofix AS (
  SELECT ctid
  FROM ""public"".""MatchClubs""
  WHERE ""Id"" IS NULL
)
UPDATE ""public"".""MatchClubs"" mc
SET ""Id"" = nextval('""public"".""MatchClubs_Id_seq""')
FROM tofix f
WHERE mc.ctid = f.ctid;

WITH ranked AS (
  SELECT ctid, ""Id"",
         ROW_NUMBER() OVER (PARTITION BY ""Id"" ORDER BY ctid) AS rn
  FROM ""public"".""MatchClubs""
),
dups AS (
  SELECT r.ctid
  FROM ranked r
  WHERE r.rn > 1
)
UPDATE ""public"".""MatchClubs"" mc
SET ""Id"" = nextval('""public"".""MatchClubs_Id_seq""')
FROM dups d
WHERE mc.ctid = d.ctid;
");

            // 2) DEFAULT e OWNED BY (note o nextval com aspas internas)
            migrationBuilder.Sql(@"
ALTER TABLE ""public"".""MatchClubs""
  ALTER COLUMN ""Id"" DROP DEFAULT;

ALTER TABLE ""public"".""MatchClubs""
  ALTER COLUMN ""Id"" SET DEFAULT nextval('""public"".""MatchClubs_Id_seq""');

DO $$
BEGIN
  PERFORM 1
  FROM pg_depend d
  JOIN pg_class s ON s.oid = d.objid
  JOIN pg_class t ON t.oid = d.refobjid
  JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = d.refobjsubid
  JOIN pg_namespace n ON n.oid = t.relnamespace
  WHERE s.relkind = 'S'
    AND s.relname = 'MatchClubs_Id_seq'
    AND n.nspname = 'public'
    AND t.relname = 'MatchClubs'
    AND a.attname = 'Id';

  IF NOT FOUND THEN
    ALTER SEQUENCE ""public"".""MatchClubs_Id_seq""
      OWNED BY ""public"".""MatchClubs"".""Id"";
  END IF;
END $$;
");

            // 3) setval com aspas internas
            migrationBuilder.Sql(@"
SELECT setval('""public"".""MatchClubs_Id_seq""',
              COALESCE((SELECT MAX(""Id"") FROM ""public"".""MatchClubs""), 0),
              true);
");

            // 4) recria PK em Id
            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF EXISTS (
    SELECT 1
    FROM pg_constraint con
    JOIN pg_class rel ON rel.oid = con.conrelid
    JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
    WHERE con.contype = 'p'
      AND con.conname = 'PK_MatchClubs'
      AND rel.relname = 'MatchClubs'
      AND nsp.nspname = 'public'
  ) THEN
    ALTER TABLE ""public"".""MatchClubs""
      DROP CONSTRAINT ""PK_MatchClubs"";
  END IF;

  ALTER TABLE ""public"".""MatchClubs""
    ADD CONSTRAINT ""PK_MatchClubs"" PRIMARY KEY (""Id"");
END $$;
");

            // 5) dedup (MatchId,ClubId) e índice único
            migrationBuilder.Sql(@"
WITH d AS (
  SELECT ctid,
         ROW_NUMBER() OVER (PARTITION BY ""MatchId"", ""ClubId"" ORDER BY ""Id"" DESC) AS rn
  FROM ""public"".""MatchClubs""
)
DELETE FROM ""public"".""MatchClubs"" mc
USING d
WHERE mc.ctid = d.ctid AND d.rn > 1;

CREATE UNIQUE INDEX IF NOT EXISTS ""UX_MatchClubs_MatchId_ClubId""
  ON ""public"".""MatchClubs"" (""MatchId"", ""ClubId"");
");
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove o índice único (par Match/Club)
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""UX_MatchClubs_MatchId_ClubId"";");

            // (Opcional) Se quiser voltar ao estado anterior, você poderia:
            // - Remover o DEFAULT da coluna Id
            // - Remover a PK em Id e recriar a PK composta
            // Eu não recomendo voltar, mas segue um exemplo minimalista (comentado):
            /*
            migrationBuilder.Sql(@"
            DO $$
            BEGIN
              IF EXISTS (
                SELECT 1
                FROM pg_constraint con
                JOIN pg_class rel ON rel.oid = con.conrelid
                JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
                WHERE con.contype = 'p'
                  AND con.conname = 'PK_MatchClubs'
                  AND rel.relname = 'MatchClubs'
                  AND nsp.nspname = 'public'
              ) THEN
                ALTER TABLE ""public"".""MatchClubs"" DROP CONSTRAINT ""PK_MatchClubs"";
              END IF;

              ALTER TABLE ""public"".""MatchClubs""
                ADD CONSTRAINT ""PK_MatchClubs"" PRIMARY KEY (""MatchId"", ""ClubId"");
            END $$;

            ALTER TABLE ""public"".""MatchClubs""
              ALTER COLUMN ""Id"" DROP DEFAULT;

            -- Remover OWNED BY (opcional)
            DO $$
            DECLARE seq regclass;
            BEGIN
              SELECT 'public.MatchClubs_Id_seq'::regclass INTO seq;
              IF seq IS NOT NULL THEN
                ALTER SEQUENCE ""public"".""MatchClubs_Id_seq"" OWNED BY NONE;
              END IF;
            END $$;
            ");
            */
        }
    }
}
