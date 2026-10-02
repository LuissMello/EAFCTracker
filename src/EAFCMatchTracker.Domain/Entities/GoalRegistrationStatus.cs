namespace EAFCMatchTracker.Domain.Entities;

/// <summary>Situação de um registro antecipado de gols ("caderno") em relação à partida real.</summary>
public enum GoalRegistrationStatus
{
    /// <summary>Aguardando a partida ser buscada na EA.</summary>
    Pending = 0,

    /// <summary>Vinculado a uma partida; os links de gols foram materializados em MatchGoalLinks.</summary>
    Linked = 1,

    /// <summary>Partida encontrada, mas o vínculo precisa de conferência humana (ambiguidade ou dados inconsistentes).</summary>
    NeedsReview = 2,

    /// <summary>Nenhuma partida apareceu dentro do prazo configurado.</summary>
    Expired = 3
}
