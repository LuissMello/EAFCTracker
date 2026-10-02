using System.Globalization;
using System.Text.Json;

namespace EAFCMatchTracker.Application.Services;

/// <summary>
/// Leitura tolerante de JSON da EA: nomes de propriedade sem diferenciar maiúsculas e valores que podem vir como
/// string ("12") ou número (12).
/// </summary>
internal static class EaJson
{
    public static JsonElement? Prop(this JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object) return null;
        foreach (var p in obj.EnumerateObject())
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                return p.Value;
        return null;
    }

    public static string? Str(this JsonElement obj, string name)
    {
        var v = obj.Prop(name);
        if (v is null) return null;
        return v.Value.ValueKind switch
        {
            JsonValueKind.String => v.Value.GetString(),
            JsonValueKind.Number => v.Value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    public static int? Int(this JsonElement obj, string name)
    {
        var v = obj.Prop(name);
        if (v is null) return null;
        if (v.Value.ValueKind == JsonValueKind.Number && v.Value.TryGetInt32(out var n)) return n;
        if (v.Value.ValueKind == JsonValueKind.String
            && int.TryParse(v.Value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s)) return s;
        return null;
    }

    public static long? Long(this JsonElement obj, string name)
    {
        var v = obj.Prop(name);
        if (v is null) return null;
        if (v.Value.ValueKind == JsonValueKind.Number && v.Value.TryGetInt64(out var n)) return n;
        if (v.Value.ValueKind == JsonValueKind.String
            && long.TryParse(v.Value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s)) return s;
        return null;
    }
}
