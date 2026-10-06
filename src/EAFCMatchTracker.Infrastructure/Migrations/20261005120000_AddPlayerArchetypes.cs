using System;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EAFCMatchTracker.Migrations;

/// <summary>
/// Catálogo de arquétipos de jogador (id da EA -> nome/sigla/grupo editáveis no Admin).
/// ADITIVA: só cria a tabela PlayerArchetypes; nenhuma tabela existente é alterada. O código tolera a ausência
/// desta tabela (a API em modo somente leitura contra um banco sem a migration cai para "Arquétipo #id").
/// </summary>
[DbContext(typeof(EAFCContext))]
[Migration("20261005120000_AddPlayerArchetypes")]
public sealed class AddPlayerArchetypes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PlayerArchetypes",
            columns: table => new
            {
                Id = table.Column<short>(type: "smallint", nullable: false),
                Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                ShortName = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                PositionGroup = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlayerArchetypes", x => x.Id);
            });

        // Nomes conhecidos dos 13 arquétipos (id da EA -> nome/grupo). Idempotente: só insere ids AUSENTES, nunca sobrescreve
        // linha editada no Admin (o mesmo mapa é o padrão em código: ArchetypeDefaults.Known).
        migrationBuilder.Sql(@"INSERT INTO ""PlayerArchetypes"" (""Id"", ""Name"", ""ShortName"", ""PositionGroup"", ""UpdatedAtUtc"") VALUES
  (1, 'Shot Stopper', NULL, 'GOLEIRO', NOW()),
  (2, 'Sweeper Keeper', NULL, 'GOLEIRO', NOW()),
  (3, 'Progressor', NULL, 'DEFESA', NOW()),
  (4, 'Boss', NULL, 'DEFESA', NOW()),
  (5, 'Disruptor', NULL, 'DEFESA', NOW()),
  (6, 'Marauder', NULL, 'DEFESA', NOW()),
  (7, 'Recycler', NULL, 'MEIO', NOW()),
  (8, 'Maestro', NULL, 'MEIO', NOW()),
  (9, 'Creator', NULL, 'MEIO', NOW()),
  (10, 'Spark', NULL, 'MEIO', NOW()),
  (11, 'Magician', NULL, 'ATAQUE', NOW()),
  (12, 'Finisher', NULL, 'ATAQUE', NOW()),
  (13, 'Target', NULL, 'ATAQUE', NOW())
ON CONFLICT (""Id"") DO NOTHING;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PlayerArchetypes");
    }
}
