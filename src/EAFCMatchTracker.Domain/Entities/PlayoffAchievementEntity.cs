using System.ComponentModel.DataAnnotations;

namespace EAFCMatchTracker.Domain.Entities;

public class PlayoffAchievementEntity
{
    [Key]
    public long Id { get; set; }

    // Chave de ligação lógica com MatchClubEntity (mesmo ClubId); não criamos FK para evitar cardinalidade errada.
    public long ClubId { get; set; }

    // Identifica unicamente a "temporada" no EAFC (unique por ClubId + SeasonId)
    public string SeasonId { get; set; } = "";

    // Informações retornadas pela API
    public string? SeasonName { get; set; }
    public string? BestDivision { get; set; }
    public string? BestFinishGroup { get; set; }

    // Auditoria
    public DateTime RetrievedAtUtc { get; set; }   // quando criamos o registro
    public DateTime UpdatedAtUtc { get; set; }     // última atualização via ingest
}
