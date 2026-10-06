using EAFCMatchTracker.Application.Dtos;

namespace EAFCMatchTracker.Application.Interfaces.Services;

/// <summary>Arquétipos de jogador: catálogo público, resumo por clube e edição no Admin.</summary>
public interface IArchetypeService
{
    /// <summary>Catálogo + ids observados nas partidas (cache de 10 min).</summary>
    Task<List<ArchetypeRef>> GetCatalogAsync(CancellationToken ct);

    /// <summary>Uso de cada arquétipo pelo clube no período (somente leitura, cache de 60 s).</summary>
    Task<ArchetypeSummaryDto> GetSummaryAsync(long clubId, DateOnly? from, DateOnly? to, int? gameVersion, CancellationToken ct);

    /// <summary>Resumo com filtros opcionais de arquétipo e de grupo da posição (AND).</summary>
    Task<ArchetypeSummaryDto> GetSummaryAsync(
        long clubId, DateOnly? from, DateOnly? to, int? gameVersion, int? archetypeId, string? positionGroup, CancellationToken ct);

    /// <summary>Todos os ids observados + os do catálogo, com contagens e top 3 jogadores (Admin).</summary>
    Task<List<AdminArchetypeDto>> GetAdminListAsync(CancellationToken ct);

    /// <summary>Upsert do nome/sigla/grupo (já validado por <c>ArchetypeService.Validate</c>). Lança <see cref="ArchetypeTableMissingException"/> sem a tabela.</summary>
    Task<AdminArchetypeDto> UpdateAsync(int id, AdminArchetypeUpdateDto normalized, CancellationToken ct);
}

/// <summary>A tabela PlayerArchetypes ainda não existe neste banco (migration AddPlayerArchetypes pendente).</summary>
public sealed class ArchetypeTableMissingException : Exception
{
    public ArchetypeTableMissingException(Exception inner)
        : base("A tabela PlayerArchetypes ainda não existe neste banco (migration AddPlayerArchetypes pendente).", inner) { }
}
