namespace EAFCMatchTracker.Application.Dtos;

/// <summary>
/// Mapeamento centralizado de IDs de eventos do motor do EA FC para
/// labels e categorias exibidas no frontend.
/// 
/// Regras adotadas:
/// - IDs com correspondência fortemente validada foram mantidos como definitivos
/// - IDs ainda não totalmente cravados permanecem com o melhor rótulo atual já usado no sistema
/// - Novos eventos confirmados devem ser adicionados aqui
/// </summary>
public static class MatchEventDefinitions
{
    public static readonly IReadOnlyList<EventDefinitionDto> All = new List<EventDefinitionDto>
    {
        // ── Resumo ────────────────────────────────────────────────────────
        new() { Id = "24",  Label = "Finalizações",           Category = "Resumo" },
        new() { Id = "97",  Label = "Toques na bola",         Category = "Resumo" },
        new() { Id = "214", Label = "Gols",                   Category = "Resumo" },
        new() { Id = "11",  Label = "Assistências",           Category = "Resumo" },

        // ── Ataque ────────────────────────────────────────────────────────
        new() { Id = "25",  Label = "Finalizações no alvo",   Category = "Ataque" },
        new() { Id = "26",  Label = "Finalizações fora",      Category = "Ataque" },
        new() { Id = "104", Label = "Chances criadas",        Category = "Ataque" },
        new() { Id = "109", Label = "Passes-chave",           Category = "Ataque" },

        // ── Passe ─────────────────────────────────────────────────────────
        new() { Id = "30",  Label = "Passes curtos",          Category = "Passe" },
        new() { Id = "31",  Label = "Passes médios",          Category = "Passe" },
        new() { Id = "32",  Label = "Passes longos",          Category = "Passe" },

        // ── Posse de bola ─────────────────────────────────────────────────
        new() { Id = "215", Label = "Conduções",              Category = "Posse de bola" },
        new() { Id = "216", Label = "Conduções c/ proteção",  Category = "Posse de bola" },
        new() { Id = "217", Label = "Dribles certos",         Category = "Posse de bola" },
        new() { Id = "218", Label = "Dribles errados",        Category = "Posse de bola" },
        new() { Id = "219", Label = "Perdas de posse",        Category = "Posse de bola" },

        // ── Defesa ────────────────────────────────────────────────────────
        new() { Id = "182", Label = "Desarmes tentados",      Category = "Defesa" },
        new() { Id = "164", Label = "Desarmes certos",        Category = "Defesa" },
        new() { Id = "183", Label = "Ações defensivas vencidas", Category = "Defesa" },
        new() { Id = "184", Label = "Bloqueios",              Category = "Defesa" },
        new() { Id = "186", Label = "Interceptações",         Category = "Defesa" },
        new() { Id = "202", Label = "Carrinhos",              Category = "Defesa" },

        // ── Movimentação ──────────────────────────────────────────────────
        new() { Id = "211", Label = "Corridas",               Category = "Movimentação" },
        new() { Id = "212", Label = "Sprints",                Category = "Movimentação" },
    };

    /// <summary>
    /// Categorias na ordem de exibição das abas.
    /// </summary>
    public static readonly IReadOnlyList<string> Categories = new List<string>
    {
        "Resumo",
        "Ataque",
        "Passe",
        "Posse de bola",
        "Defesa",
        "Movimentação"
    };

    private static readonly HashSet<string> _knownIds =
        All.Select(d => d.Id).ToHashSet();

    public static bool IsKnown(string id) => _knownIds.Contains(id);

    public static EventDefinitionDto? GetById(string id) =>
        All.FirstOrDefault(x => x.Id == id);
}