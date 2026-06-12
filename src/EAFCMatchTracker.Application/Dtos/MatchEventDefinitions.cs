namespace EAFCMatchTracker.Application.Dtos;

/// <summary>
/// Mapeamento centralizado de IDs de eventos do motor do EA FC para
/// labels e categorias exibidas no frontend.
///
/// Metodologia de validação (maio/2026):
///   Correlação de Pearson + correspondência exata event_val × campos do JSON corrigido
///   (16 jogadores, 3–5 partidas reais do banco).
///
/// Legenda de confiança:
///   ✓ confirmed  — match direto >= 73% OU correlação >= 0.94 OU caso discriminante confirmado
///   ~ probable   — evidência forte mas não 100% exata (>= 3× match ou correlação >= 0.50)
///   ✗ ambiguous  — hipótese com pouca evidência, valor sempre=1, ou candidatos conflitantes
///
/// IDs que aparecem nas partidas mas NÃO estão aqui são exibidos na aba "Desconhecidos".
/// </summary>
public static class MatchEventDefinitions
{
    public static readonly IReadOnlyList<EventDefinitionDto> All = new List<EventDefinitionDto>
    {
        // ── Resumo ────────────────────────────────────────────────────────────
        // ✓ 98.6% match com Goals + 100% via goal links
        new() { Id = "214", Label = "Gols",                            Category = "Resumo",       Confidence = "confirmed" },
        // ✓ 98.7% match com Assists + 97.9% via goal links
        new() { Id = "11",  Label = "Assistências",                    Category = "Resumo",       Confidence = "confirmed" },
        // ✓ 84.8% match pré-assists via goal links (n=355)
        new() { Id = "115", Label = "Pré-assistências",                Category = "Resumo",       Confidence = "confirmed" },

        // ── Quebra de linha (drible/passe que ultrapassa uma linha defensiva) ─
        // Correlação com goal links é consequência: quem quebra a linha cria chance de gol.
        // Valores disponíveis são sempre 1 — distinção exata por zona/direção aguarda mais dados.
        // ~ 2× match pb.quebra_linha_meio_campo_pelo_meio (Zaga@460448, Zaga@270186)
        new() { Id = "118", Label = "Quebra de linha (meio/centro)",   Category = "Resumo",       Confidence = "probable"  },
        // ~ 1× match pb.quebra_linha_defesa_pelos_lados (Zaga@980239) + goal links 88%
        new() { Id = "238", Label = "Quebra de linha (defesa/lados)",  Category = "Resumo",       Confidence = "probable"  },
        // ~ 1× match pb.quebra_linha_meio_lados (Zaga@460448) + goal links 100% (n=19)
        new() { Id = "21",  Label = "Quebra de linha (meio/lados)",    Category = "Resumo",       Confidence = "probable"  },
        // ~ 1× match pb.quebra_linha_meio_campo_pelo_meio (Zaga@270186); pode ser sub-tipo de 118
        new() { Id = "104", Label = "Quebra de linha (meio/centro alt.)", Category = "Resumo",    Confidence = "probable"  },
        // ✗ goal links 100% (n=11) mas não aparece nas 3 partidas analisadas — zona não identificada
        new() { Id = "203", Label = "Quebra de linha (tipo A)",        Category = "Resumo",       Confidence = "ambiguous" },
        // ✗ goal links 93.3% (n=49) mas não aparece nas partidas atuais
        new() { Id = "123", Label = "Quebra de linha (tipo D)",        Category = "Resumo",       Confidence = "ambiguous" },
        // ✗ goal links 100% (n=12) mas não aparece nas partidas atuais
        new() { Id = "117", Label = "Quebra de linha (tipo G)",        Category = "Resumo",       Confidence = "ambiguous" },

        // ── Finalizações (Resumo) ─────────────────────────────────────────────
        // ~ 2× match discriminante: Zaga@460448 ev=5=finalizacoes=5 ≠ disputas_aereas=4
        new() { Id = "106", Label = "Total de finalizações",           Category = "Resumo",       Confidence = "probable"  },
        // ~ r=0.851, 7× match exato com finalizacoes.certas
        new() { Id = "217", Label = "Finalizações",                    Category = "Resumo",       Confidence = "probable"  },
        // ~ r=0.761, 42.9% match com Shots
        new() { Id = "13",  Label = "Finalizações no alvo",            Category = "Resumo",       Confidence = "probable"  },
        // ~ 4× match exato com finalizacoes.certas; sub-tipo de 217
        new() { Id = "211", Label = "Finalizações certas",             Category = "Resumo",       Confidence = "probable"  },
        // ✗ avg≈12, r_passes≈0.58 — candidato a toques totais, sem match direto
        new() { Id = "97",  Label = "Toques na bola",                  Category = "Resumo",       Confidence = "ambiguous" },

        // ── Ataque ────────────────────────────────────────────────────────────
        // ~ padrão confirmado: PEDRO=265:5, 8:4 → 265=tentativas totais; 8=ganhos (sempre <=265)
        new() { Id = "265", Label = "Disputas aéreas",                 Category = "Ataque",       Confidence = "probable"  },
        // ~ sempre <= event265; cresce com gols do Pedro (behavioral confirmation)
        new() { Id = "8",   Label = "Duelos aéreos ganhos",            Category = "Ataque",       Confidence = "probable"  },
        // ~ val7=1 com Goals=0 em 29% dos casos → tentativa, não gol; PEDRO domina (44%)
        new() { Id = "7",   Label = "Finalizações de cabeça",          Category = "Ataque",       Confidence = "probable"  },
        // ~ 5× match exato com finalizacoes.normal
        new() { Id = "100", Label = "Finalizações normais",            Category = "Ataque",       Confidence = "probable"  },
        // ~ 4× match exato com finalizacoes.bloqueadas — único candidato forte
        new() { Id = "12",  Label = "Finalizações bloqueadas",         Category = "Ataque",       Confidence = "probable"  },
        // ~ 3× match exato com finalizacoes.colocada
        new() { Id = "18",  Label = "Finalizações colocadas",          Category = "Ataque",       Confidence = "probable"  },
        // ~ 3× match exato com finalizacoes.erradas
        new() { Id = "19",  Label = "Finalizações erradas",            Category = "Ataque",       Confidence = "probable"  },
        // ✗ r=0.519, não aumenta com gols — provável sub-tipo de finalização sem match direto
        new() { Id = "202", Label = "Finalizações (área)",             Category = "Ataque",       Confidence = "ambiguous" },
        // ✗ r=0.497 — candidato a chutes de longe, sem match direto confirmado
        new() { Id = "177", Label = "Finalizações de longe",           Category = "Ataque",       Confidence = "ambiguous" },
        // ~ 5× match exato com posses_de_bola_ganhas
        new() { Id = "109", Label = "Posses de bola ganhas",           Category = "Ataque",       Confidence = "probable"  },
        // ✗ sempre valor=1, aparece em forwards de ambos os times — hipótese de kickoff
        new() { Id = "205", Label = "Saída de bola (tipo 2)",          Category = "Ataque",       Confidence = "ambiguous" },
        // ✗ padrão idêntico ao 205, menos frequente — sem match direto
        new() { Id = "213", Label = "Saída de bola (tipo 3)",          Category = "Ataque",       Confidence = "ambiguous" },

        // ── Passe ─────────────────────────────────────────────────────────────
        // ✓ 73.3% match global com Passesmade; 90.5% excluindo event153 (r=0.892)
        new() { Id = "215", Label = "Passes completos",                Category = "Passe",        Confidence = "confirmed" },
        // ✓ 7× match exato com dribles_completos — evidência mais forte do dataset
        new() { Id = "174", Label = "Dribles completos",               Category = "Passe",        Confidence = "confirmed" },
        // ~ pedido de bola (call for ball); gera +1 em Passesmade em ~59% dos casos
        new() { Id = "153", Label = "Pedidos de bola",                 Category = "Passe",        Confidence = "probable"  },
        // ✗ avg≈8.6, alta correlação com passes — sem match direto em campo específico
        new() { Id = "30",  Label = "Passes curtos",                   Category = "Passe",        Confidence = "ambiguous" },
        // ~ match em algumas partidas, inconsistente em outras — possível sub-tipo de enfiada
        new() { Id = "24",  Label = "Enfiada",                         Category = "Passe",        Confidence = "probable"  },
        // ✗ correlação moderada; evidência conflitante com Enfiada por cima
        new() { Id = "31",  Label = "Passes longos",                   Category = "Passe",        Confidence = "ambiguous" },
        // ✓ 4× match com posse_de_bola.superando_oponentes (discriminado pelo JSON corrigido)
        new() { Id = "112", Label = "Superações de oponente",          Category = "Passe",        Confidence = "confirmed" },
        // ✓ 4× match exato com passes.interceptados
        new() { Id = "25",  Label = "Passes interceptados",            Category = "Passe",        Confidence = "confirmed" },
        // ~ 3× match val=7 (discriminante: Zaga@980239 ev=7=interceptados≠disputas_aereas)
        new() { Id = "107", Label = "Passes interceptados (tipo 2)",   Category = "Passe",        Confidence = "probable"  },
        // ~ 2× match exato com passes.para_impedimento — único candidato
        new() { Id = "39",  Label = "Passes para impedimento",         Category = "Passe",        Confidence = "probable"  },
        // ~ 2× match com posse_de_bola.adiantadas (forward carries)
        new() { Id = "101", Label = "Adiantadas",                      Category = "Passe",        Confidence = "probable"  },
        // ~ 4× match com passes.tentativa_pelos_lados
        new() { Id = "143", Label = "Tentativas pelos lados",          Category = "Passe",        Confidence = "probable"  },
        // ~ 3× match com passes.enfiada_por_cima — sub-tipo de 212
        new() { Id = "124", Label = "Enfiada por cima (tipo 2)",       Category = "Passe",        Confidence = "probable"  },
        // ✗ app=14 (muito frequente), 2× match — muitos candidatos concorrentes
        new() { Id = "175", Label = "Passes sob pressão",              Category = "Passe",        Confidence = "ambiguous" },
        // ✓ 3× match exato com passes.superacoes_com_passes (JSON corrigido)
        new() { Id = "26",  Label = "Superações com passes",           Category = "Passe",        Confidence = "confirmed" },
        // ✗ sub-tipo de superação com passe — sem match direto confirmado
        new() { Id = "27",  Label = "Superação com passe (tipo 2)",    Category = "Passe",        Confidence = "ambiguous" },
        // ✗ sub-tipo de superação com passe — sem match direto confirmado
        new() { Id = "28",  Label = "Superação com passe (tipo 3)",    Category = "Passe",        Confidence = "ambiguous" },
        // ✗ avg≈5.2, correlação semelhante ao 26 — candidato sem match direto
        new() { Id = "176", Label = "Passes médios (alt.)",            Category = "Passe",        Confidence = "ambiguous" },
        // ~ 5× match exato com passes.passes_importantes
        new() { Id = "32",  Label = "Passes importantes",              Category = "Passe",        Confidence = "probable"  },
        // ✗ avg≈3.4, correlação moderada — sem campo JSON identificado
        new() { Id = "34",  Label = "Passes (tipo 4)",                 Category = "Passe",        Confidence = "ambiguous" },
        // ✓ match perfeito em 2 partidas; confirmado também como "Quebra por cima do meio-campo"
        new() { Id = "212", Label = "Enfiada por cima",                Category = "Passe",        Confidence = "confirmed" },

        // ── Posse de bola ─────────────────────────────────────────────────────
        // ~ 4× match com disputas_no_ataque_vencidas
        new() { Id = "216", Label = "Disputas no ataque vencidas",     Category = "Posse de bola", Confidence = "probable" },
        // ✗ avg≈7.2, presente em quase todos os jogadores — sem match direto isolado
        new() { Id = "219", Label = "Perdas de posse",                 Category = "Posse de bola", Confidence = "ambiguous"},
        // ~ 6× match com passes.enfiada; alguns conflitos com rasteiro em partidas específicas
        new() { Id = "152", Label = "Enfiada",                         Category = "Posse de bola", Confidence = "probable" },

        // ── Defesa ────────────────────────────────────────────────────────────
        // ✓ 94.2% match com Tacklesmade, r=0.946
        new() { Id = "164", Label = "Desarmes certos",                 Category = "Defesa",       Confidence = "confirmed" },
        // ✓ 89.1% match com Tacklesmade, r=0.917
        new() { Id = "0",   Label = "Desarmes (pressão)",              Category = "Defesa",       Confidence = "confirmed" },
        // ✓ 87.2% match com Tacklesmade, r=0.893
        new() { Id = "121", Label = "Desarmes (bola recuperada)",      Category = "Defesa",       Confidence = "confirmed" },
        // ✓ 85.3% match com Tacklesmade, r=0.882
        new() { Id = "229", Label = "Desarmes (disputa ganha)",        Category = "Defesa",       Confidence = "confirmed" },
        // ~ 56.7% match com Tacklesmade, r=0.634
        new() { Id = "158", Label = "Desarmes (complementar)",         Category = "Defesa",       Confidence = "probable"  },
        // ~ r_tackleattempts=0.795 — melhor candidato para tentativas de desarme
        new() { Id = "1",   Label = "Tentativas de desarme",           Category = "Defesa",       Confidence = "probable"  },
        // ~ r_tackleattempts=0.652 — tentativas de desarme tipo 2
        new() { Id = "163", Label = "Tentativas de desarme (alt.)",    Category = "Defesa",       Confidence = "probable"  },
        // ✓ 6× match exato com interceptacoes; discriminante: Zaga@580319 ev=5=intercept≠duelos
        new() { Id = "6",   Label = "Interceptações (duelos)",         Category = "Defesa",       Confidence = "confirmed" },
        // ~ 4× match com disputas_aereas_vencidas
        new() { Id = "29",  Label = "Disputas aéreas vencidas",        Category = "Defesa",       Confidence = "probable"  },
        // ~ 2× match ÚNICO com disputas_na_defesa_vencidas (sem concorrentes)
        new() { Id = "151", Label = "Disputas na defesa vencidas (sub)", Category = "Defesa",     Confidence = "probable"  },
        // ✓ match perfeito em 2 jogos; ausência também confirmada (event=0 ↔ campo=0)
        new() { Id = "186", Label = "Interceptações",                  Category = "Defesa",       Confidence = "confirmed" },
        // ✗ candidato a bloqueios — sem match direto robusto
        new() { Id = "184", Label = "Bloqueios",                       Category = "Defesa",       Confidence = "ambiguous" },
        // ✗ candidato a carrinhos — sem match direto robusto
        new() { Id = "182", Label = "Carrinhos",                       Category = "Defesa",       Confidence = "ambiguous" },
        // ~ 4× match com disputas_na_defesa_vencidas
        new() { Id = "183", Label = "Duelos defensivos vencidos",      Category = "Defesa",       Confidence = "probable"  },

        // ── Goleiro ───────────────────────────────────────────────────────────
        // ✓ 96.5% match com GoodDirectionSaves
        new() { Id = "267", Label = "Defesas na direção certa",        Category = "Goleiro",      Confidence = "confirmed" },
        // ~ r_gooddir=0.727, r_saves=0.686
        new() { Id = "10",  Label = "Defesas difíceis",                Category = "Goleiro",      Confidence = "probable"  },
        // ~ r_saves=0.644, r_gooddir=0.705
        new() { Id = "50",  Label = "Defesas (tipo 2)",                Category = "Goleiro",      Confidence = "probable"  },
        // ~ r_saves=0.621, r_gooddir=0.456
        new() { Id = "49",  Label = "Defesas (tipo 3)",                Category = "Goleiro",      Confidence = "probable"  },
        // ✗ r=0.467 apenas — evidência fraca
        new() { Id = "99",  Label = "Defesas (tipo 4)",                Category = "Goleiro",      Confidence = "ambiguous" },

        // ── Disciplina ────────────────────────────────────────────────────────
        // ✓ avg=1.04 sem RC; avg=1.71 com RC (2 amarelos → expulsão); r_redcards=0.627
        new() { Id = "95",  Label = "Cartão amarelo",                  Category = "Disciplina",   Confidence = "confirmed" },
        // ✓ 84.8% das ocorrências são Redcards=1; avg=1.00 (sempre valor 1)
        new() { Id = "96",  Label = "Expulsão",                        Category = "Disciplina",   Confidence = "confirmed" },
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
        "Goleiro",
        "Movimentação",
        "Disciplina",
    };

    private static readonly HashSet<string> _knownIds =
        All.Select(d => d.Id).ToHashSet();

    public static bool IsKnown(string id) => _knownIds.Contains(id);

    public static EventDefinitionDto? GetById(string id) =>
        All.FirstOrDefault(x => x.Id == id);
}
