using System;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EAFCMatchTracker.Migrations;

/// <summary>
/// ADITIVA: três colunas anuláveis em GoalRegistrations (FinishedAt, SuggestedMatchId, DismissedMatchId). Nenhuma outra
/// tabela/coluna é alterada. Sem FK para Matches de propósito (o linker limpa sugestões de partidas apagadas).
/// </summary>
[DbContext(typeof(EAFCContext))]
[Migration("20261008120000_GoalRegistrationFinishAndSuggestion")]
public sealed class GoalRegistrationFinishAndSuggestion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "FinishedAt", table: "GoalRegistrations", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<long>(
            name: "SuggestedMatchId", table: "GoalRegistrations", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<long>(
            name: "DismissedMatchId", table: "GoalRegistrations", type: "bigint", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "FinishedAt", table: "GoalRegistrations");
        migrationBuilder.DropColumn(name: "SuggestedMatchId", table: "GoalRegistrations");
        migrationBuilder.DropColumn(name: "DismissedMatchId", table: "GoalRegistrations");
    }
}
