using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EAFCMatchTracker.Migrations
{
    /// <inheritdoc />
    public partial class AjustandoDependencias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =========================
            // 0) DROPS CONDICIONAIS / INDEX
            // =========================
            migrationBuilder.Sql(@"
DROP INDEX IF EXISTS ""IX_PlayerMatchStats_PlayerEntityId"";
CREATE INDEX IF NOT EXISTS ""IX_PlayerMatchStats_PlayerEntityId""
ON ""public"".""PlayerMatchStats""(""PlayerEntityId"");  -- não-único
");

            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_MatchPlayers_PlayerMatchStats_PlayerMatchStatsEntityId') THEN
    ALTER TABLE ""public"".""MatchPlayers""
      DROP CONSTRAINT ""FK_MatchPlayers_PlayerMatchStats_PlayerMatchStatsEntityId"";
  END IF;
END $$;");

            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_PlayerMatchStats_Players_PlayerEntityId') THEN
    ALTER TABLE ""public"".""PlayerMatchStats""
      DROP CONSTRAINT ""FK_PlayerMatchStats_Players_PlayerEntityId"";
  END IF;
END $$;");

            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Players_PlayerMatchStats_PlayerMatchStatsId') THEN
    ALTER TABLE ""public"".""Players""
      DROP CONSTRAINT ""FK_Players_PlayerMatchStats_PlayerMatchStatsId"";
  END IF;
END $$;");

            // =========================
            // 1) HIGIENIZAÇÃO DE DADOS
            // =========================
            // 1a) Criar snapshots de stats para MatchPlayers órfãos (34 zeros ::smallint)
            migrationBuilder.Sql(@"
WITH missing AS (
  SELECT DISTINCT mp.""PlayerEntityId""
  FROM ""public"".""MatchPlayers"" mp
  LEFT JOIN ""public"".""PlayerMatchStats"" s ON s.""Id"" = mp.""PlayerMatchStatsEntityId""
  WHERE s.""Id"" IS NULL
),
ins AS (
  INSERT INTO ""public"".""PlayerMatchStats"" (
      ""PlayerEntityId"",
      ""Aceleracao"",""Pique"",""Finalizacao"",""Falta"",""Cabeceio"",""ForcaDoChute"",""ChuteLonge"",""Voleio"",""Penalti"",
      ""Visao"",""Cruzamento"",""Lancamento"",""PasseCurto"",""Curva"",""Agilidade"",""Equilibrio"",""PosAtaqueInutil"",
      ""ControleBola"",""Conducao"",""Interceptacaos"",""NocaoDefensiva"",""DivididaEmPe"",""Carrinho"",
      ""Impulsao"",""Folego"",""Forca"",""Reacao"",""Combatividade"",""Frieza"",
      ""ElasticidadeGL"",""ManejoGL"",""ChuteGL"",""ReflexosGL"",""PosGL""
  )
  SELECT
      m.""PlayerEntityId"",
      0::smallint, 0::smallint, 0::smallint, 0::smallint, 0::smallint, 0::smallint, 0::smallint, 0::smallint, 0::smallint,
      0::smallint, 0::smallint, 0::smallint, 0::smallint, 0::smallint, 0::smallint, 0::smallint, 0::smallint,
      0::smallint, 0::smallint, 0::smallint, 0::smallint, 0::smallint, 0::smallint,
      0::smallint, 0::smallint, 0::smallint, 0::smallint, 0::smallint, 0::smallint,
      0::smallint, 0::smallint, 0::smallint, 0::smallint, 0::smallint
  FROM missing m
  RETURNING ""Id"", ""PlayerEntityId""
)
UPDATE ""public"".""Players"" p
SET ""PlayerMatchStatsId"" = ins.""Id""
FROM ins
WHERE p.""Id"" = ins.""PlayerEntityId"";");

            // 1b) Atualizar MatchPlayers órfãos → snapshot mais recente do jogador (sem JOIN referenciando mp)
            migrationBuilder.Sql(@"
WITH latest AS (
  SELECT DISTINCT ON (s.""PlayerEntityId"")
         s.""PlayerEntityId"", s.""Id""
  FROM ""public"".""PlayerMatchStats"" s
  ORDER BY s.""PlayerEntityId"", s.""Id"" DESC
)
UPDATE ""public"".""MatchPlayers"" mp
SET ""PlayerMatchStatsEntityId"" = l.""Id""
FROM latest l
WHERE mp.""PlayerEntityId"" = l.""PlayerEntityId""
  AND NOT EXISTS (
    SELECT 1
    FROM ""public"".""PlayerMatchStats"" s
    WHERE s.""Id"" = mp.""PlayerMatchStatsEntityId""
  );");

            // 1c) Deduplicar para caber na nova PK (mantém a maior StatsId)
            migrationBuilder.Sql(@"
WITH dups AS (
  SELECT ""MatchId"",""ClubId"",""PlayerEntityId"",
         ROW_NUMBER() OVER (PARTITION BY ""MatchId"",""ClubId"",""PlayerEntityId""
                            ORDER BY ""PlayerMatchStatsEntityId"" DESC) AS rn
  FROM ""public"".""MatchPlayers""
)
DELETE FROM ""public"".""MatchPlayers"" mp
USING dups d
WHERE mp.""MatchId"" = d.""MatchId""
  AND mp.""ClubId"" = d.""ClubId""
  AND mp.""PlayerEntityId"" = d.""PlayerEntityId""
  AND d.rn > 1;");

            // =========================
            // 2) TROCAR A PK
            // =========================
            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM pg_constraint con
    JOIN pg_class rel ON rel.oid = con.conrelid
    JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
    WHERE con.contype = 'p'
      AND con.conname = 'PK_MatchPlayers'
      AND nsp.nspname = 'public'
      AND rel.relname = 'MatchPlayers'
  ) THEN
    ALTER TABLE ""public"".""MatchPlayers"" DROP CONSTRAINT ""PK_MatchPlayers"";
  END IF;
END $$;");

            migrationBuilder.Sql(@"
ALTER TABLE ""public"".""MatchPlayers""
  ADD CONSTRAINT ""PK_MatchPlayers""
  PRIMARY KEY (""MatchId"", ""ClubId"", ""PlayerEntityId"");");

            // =========================
            // 3) RECRIAR FKs (NO ACTION)
            // =========================
            migrationBuilder.Sql(@"
ALTER TABLE ""public"".""MatchPlayers""
  ADD CONSTRAINT ""FK_MatchPlayers_PlayerMatchStats_PlayerMatchStatsEntityId""
  FOREIGN KEY (""PlayerMatchStatsEntityId"")
  REFERENCES ""public"".""PlayerMatchStats""(""Id"")
  ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
ALTER TABLE ""public"".""PlayerMatchStats""
  ADD CONSTRAINT ""FK_PlayerMatchStats_Players_PlayerEntityId""
  FOREIGN KEY (""PlayerEntityId"")
  REFERENCES ""public"".""Players""(""Id"")
  ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
ALTER TABLE ""public"".""Players""
  ADD CONSTRAINT ""FK_Players_PlayerMatchStats_PlayerMatchStatsId""
  FOREIGN KEY (""PlayerMatchStatsId"")
  REFERENCES ""public"".""PlayerMatchStats""(""Id"")
  ON DELETE NO ACTION;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop FKs novas
            migrationBuilder.Sql(@"
ALTER TABLE ""public"".""MatchPlayers""      DROP CONSTRAINT IF EXISTS ""FK_MatchPlayers_PlayerMatchStats_PlayerMatchStatsEntityId"";
ALTER TABLE ""public"".""PlayerMatchStats""  DROP CONSTRAINT IF EXISTS ""FK_PlayerMatchStats_Players_PlayerEntityId"";
ALTER TABLE ""public"".""Players""           DROP CONSTRAINT IF EXISTS ""FK_Players_PlayerMatchStats_PlayerMatchStatsId"";");

            // Voltar PK (ajuste se sua PK antiga era diferente)
            migrationBuilder.Sql(@"
ALTER TABLE ""public"".""MatchPlayers""
  DROP CONSTRAINT IF EXISTS ""PK_MatchPlayers"";
ALTER TABLE ""public"".""MatchPlayers""
  ADD CONSTRAINT ""PK_MatchPlayers""
  PRIMARY KEY (""MatchId"", ""PlayerEntityId"");");
        }
    }
}
