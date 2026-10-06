using EAFCMatchTracker.Application.Services.Analytics;

namespace EAFCMatchTracker.Application.Interfaces.Services;

/// <summary>
/// Catálogo de arquétipos (id da EA -> nome/sigla/grupo) com cache de 10 minutos. NUNCA lança por falha de leitura do
/// catálogo (ex.: tabela ainda inexistente no banco): nesse caso devolve um catálogo vazio e os rótulos viram "Arquétipo #id".
/// </summary>
public interface IArchetypeCatalog
{
    Task<ArchetypeCatalogSnapshot> GetAsync(CancellationToken ct = default);

    /// <summary>Descarta o cache (chamado ao salvar no Admin).</summary>
    void Invalidate();
}
