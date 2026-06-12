namespace EAFCMatchTracker.Application.Dtos;

public class EventDefinitionDto
{
    public string Id         { get; set; } = default!;
    public string Label      { get; set; } = default!;
    public string Category   { get; set; } = default!;

    /// <summary>
    /// Nível de confiança do mapeamento deste evento.
    /// "confirmed"  → ✓  match direto >= 73% ou correlação >= 0.94
    /// "probable"   → ~  evidência forte mas não exata
    /// "ambiguous"  → ✗  hipótese com pouca evidência ou conflitante
    /// </summary>
    public string Confidence { get; set; } = "ambiguous";
}
