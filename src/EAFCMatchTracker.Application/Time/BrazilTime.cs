namespace EAFCMatchTracker.Application.Time;

/// <summary>
/// Horário de Brasília (America/Sao_Paulo) para agrupar partidas em "dias" e converter as datas locais
/// enviadas pelo frontend (YYYY-MM-DD) em limites UTC. O Brasil não tem horário de verão desde 2019;
/// se a base tzdata não existir (ex.: imagem Docker enxuta), cai para um offset fixo UTC-3.
/// </summary>
public static class BrazilTime
{
    private static readonly TimeSpan FixedOffset = TimeSpan.FromHours(-3);
    private static readonly TimeZoneInfo Zone = ResolveZone();

    private static TimeZoneInfo ResolveZone()
    {
        foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.CreateCustomTimeZone("BRT", FixedOffset, "Brasilia Time", "Brasilia Time");
    }

    /// <summary>Converte um instante UTC (Kind ignorado, tratado como UTC) para o horário local de Brasília (Kind Unspecified).</summary>
    public static DateTime ToLocal(DateTime utc)
    {
        var asUtc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(asUtc, Zone), DateTimeKind.Unspecified);
    }

    /// <summary>Dia (calendário de Brasília) em que o instante UTC ocorreu.</summary>
    public static DateOnly ToLocalDate(DateTime utc) => DateOnly.FromDateTime(ToLocal(utc));

    /// <summary>Instante UTC (Kind Utc) correspondente a 00:00 de Brasília do dia informado.</summary>
    public static DateTime StartOfLocalDayUtc(DateTime localDate) => StartOfLocalDayUtc(DateOnly.FromDateTime(localDate));

    /// <summary>Instante UTC (Kind Utc) correspondente a 00:00 de Brasília do dia informado.</summary>
    public static DateTime StartOfLocalDayUtc(DateOnly localDate)
    {
        var local = DateTime.SpecifyKind(localDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        try
        {
            return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(local, Zone), DateTimeKind.Utc);
        }
        catch (ArgumentException)
        {
            // Horário inexistente/ambíguo (lacuna de DST histórica): usa o offset fixo
            return DateTime.SpecifyKind(local - FixedOffset, DateTimeKind.Utc);
        }
    }

    /// <summary>Garante Kind=Utc (Npgsql/timestamptz rejeita Unspecified): Unspecified é tratado como UTC; Local é convertido.</summary>
    public static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    /// <summary>Idem <see cref="EnsureUtc(DateTime)"/> para valores opcionais.</summary>
    public static DateTime? EnsureUtc(DateTime? value) => value.HasValue ? EnsureUtc(value.Value) : null;
}
