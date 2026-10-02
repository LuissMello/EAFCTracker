using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Domain.Entities;

namespace EAFCMatchTracker.Application.Interfaces.Services;

/// <summary>Resultado de uma rodada do linker (contagem de registros que mudaram de situação na rodada).</summary>
public sealed record GoalLinkRunResult(int Linked, int NeedsReview, int Expired);

/// <summary>
/// Vincula registros antecipados de gols (GoalRegistrations) às partidas reais e materializa os MatchGoalLinks.
/// Idempotente: pode ser executado a qualquer momento (criação/edição, fim de cada ciclo de busca, endpoint admin).
/// </summary>
public interface IGoalRegistrationLinker
{
    /// <summary>
    /// Higiene (registros órfãos voltam a Pending; Pending antigos expiram) e vínculo dos Pending por grupo
    /// (clube, adversário). Quando <paramref name="clubId"/>/<paramref name="opponentClubId"/> são informados, apenas o
    /// grupo correspondente é processado (a higiene é sempre global).
    /// </summary>
    Task<GoalLinkRunResult> RunAsync(long? clubId = null, long? opponentClubId = null, CancellationToken ct = default);

    /// <summary>
    /// Valida uma lista de gols contra a partida real (jogadores pertencem ao clube na partida, assistência/pré-assistência
    /// coerentes, gols/assistências por jogador e total do clube não excedem o real). Retorna a mensagem (pt-BR) do
    /// primeiro problema, ou null se válida. Não altera nada.
    /// </summary>
    Task<string?> ValidateGoalsAsync(long clubId, long matchId, IReadOnlyList<GoalRegistrationDto> goals, CancellationToken ct = default);

    /// <summary>
    /// Reaplica a materialização de um registro que já tem partida atribuída (Linked/NeedsReview), a partir das linhas
    /// de gols EM MEMÓRIA do registro rastreado (inclusive alterações ainda não salvas), substituindo os MatchGoalLinks
    /// dele. NÃO chama SaveChanges: o chamador salva tudo de uma vez (edição + links + status). Se a partida não existe
    /// mais, o registro volta a Pending. Retorna a situação resultante.
    /// </summary>
    Task<GoalRegistrationStatus> ReapplyAsync(GoalRegistrationEntity registration, CancellationToken ct = default);
}
