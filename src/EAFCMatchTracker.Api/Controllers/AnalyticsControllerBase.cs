using System.Globalization;
using EAFCMatchTracker.Application.Services.Analytics;
using Microsoft.AspNetCore.Mvc;

namespace EAFCMatchTracker.Api.Controllers;

/// <summary>
/// Base das páginas analíticas públicas (noite de jogo, laboratório, retrospectiva): GETs somente leitura, sem login,
/// com rate limit por IP (política <see cref="RateLimitPolicy"/>) e validação de entrada com ProblemDetails em pt-BR.
/// </summary>
[ApiController]
public abstract class AnalyticsControllerBase : ControllerBase
{
    public const string RateLimitPolicy = "analytics-read";

    private const string DateFormat = "yyyy-MM-dd";
    private const int MaxRangeYears = 5;
    private const int MaxRangeDays = MaxRangeYears * 366;

    protected ObjectResult Fail(int status, string title, string detail) =>
        new(new ProblemDetails { Status = status, Title = title, Detail = detail }) { StatusCode = status };

    protected ObjectResult BadInput(string detail) => Fail(StatusCodes.Status400BadRequest, "Requisição inválida", detail);

    protected ObjectResult NotFoundProblem(string detail) => Fail(StatusCodes.Status404NotFound, "Não encontrado", detail);

    protected ObjectResult? ValidateClub(long clubId) =>
        clubId <= 0 ? BadInput("Informe um clubId válido.") : null;

    /// <summary>Número da edição (ex.: 27). Ausente = todas. Texto inválido ou &lt;= 0 gera erro 400.</summary>
    protected ObjectResult? ParseGameVersion(string? raw, out int? gameVersion)
    {
        gameVersion = null;
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) || v <= 0)
            return BadInput("gameVersion deve ser um número inteiro positivo (ex.: 27).");
        gameVersion = v;
        return null;
    }

    /// <summary>minMatches: padrão <paramref name="defaultValue"/>, limitado a 1..30. Texto inválido gera erro 400.</summary>
    protected ObjectResult? ParseMinMatches(string? raw, int defaultValue, out int minMatches)
    {
        minMatches = defaultValue;
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            return BadInput("minMatches deve ser um número inteiro.");
        minMatches = Math.Clamp(v, LabService.MinMatchesFloor, LabService.MinMatchesCeiling);
        return null;
    }

    /// <summary>archetypeId (opcional): inteiro de 1 a 255. Ausente/vazio = sem filtro; outro valor gera erro 400.</summary>
    protected ObjectResult? ParseArchetypeId(string? raw, out int? archetypeId)
    {
        archetypeId = null;
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) || v < 1 || v > 255)
            return BadInput("archetypeId deve ser um número inteiro entre 1 e 255.");
        archetypeId = v;
        return null;
    }

    /// <summary>positionGroup (opcional): ATAQUE, MEIO, DEFESA ou GOLEIRO (sem diferenciar maiúsculas). Ausente/vazio = sem filtro.</summary>
    protected ObjectResult? ParsePositionGroup(string? raw, out string? positionGroup)
    {
        positionGroup = null;
        if (string.IsNullOrWhiteSpace(raw)) return null;
        positionGroup = ArchetypeGroups.Normalize(raw);
        return positionGroup is null
            ? BadInput($"positionGroup deve ser um de: {string.Join(", ", ArchetypeGroups.All)}.")
            : null;
    }

    /// <summary>from/to como datas locais yyyy-MM-dd; from &gt; to ou intervalo maior que 5 anos geram erro 400.</summary>
    protected ObjectResult? ParseRange(string? rawFrom, string? rawTo, out DateOnly? from, out DateOnly? to)
    {
        from = null;
        to = null;
        if (!TryParseDate(rawFrom, out from)) return BadInput("from deve estar no formato yyyy-MM-dd.");
        if (!TryParseDate(rawTo, out to)) return BadInput("to deve estar no formato yyyy-MM-dd.");

        if (from is not null && to is not null)
        {
            if (from > to) return BadInput("A data inicial (from) não pode ser posterior à final (to).");
            if (to.Value.DayNumber - from.Value.DayNumber > MaxRangeDays)
                return BadInput($"O período máximo é de {MaxRangeYears} anos.");
        }
        return null;
    }

    private static bool TryParseDate(string? raw, out DateOnly? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;
        if (!DateOnly.TryParseExact(raw.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            || d.Year < 2000 || d.Year > 2100)
            return false;
        value = d;
        return true;
    }
}
