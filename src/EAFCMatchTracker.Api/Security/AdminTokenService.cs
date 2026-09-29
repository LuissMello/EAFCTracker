using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EAFCMatchTracker.Api.Security;

public enum AdminTokenStatus
{
    /// <summary>Assinatura válida e não expirado.</summary>
    Valid,
    /// <summary>Assinatura válida, porém expirado.</summary>
    Expired,
    /// <summary>Malformado, assinatura inválida, versão desconhecida ou serviço sem chave configurada.</summary>
    Invalid
}

public readonly record struct AdminTokenValidation(AdminTokenStatus Status, DateTimeOffset? ExpiresAtUtc);

public sealed record IssuedAdminToken(string Token, DateTimeOffset ExpiresAtUtc);

/// <summary>Emite e valida os tokens de sessão do administrador (stateless, assinados com HMAC-SHA256).</summary>
public interface IAdminTokenService
{
    /// <summary>True quando Auth:ApiKey está definida (não vazia).</summary>
    bool IsConfigured { get; }

    /// <summary>Compara em tempo constante (SHA-256 de ambos os lados) com Auth:ApiKey. Sempre false se não configurada.</summary>
    bool VerifyPassword(string? candidate);

    /// <summary>Emite um token novo. Lança InvalidOperationException se Auth:ApiKey não estiver configurada.</summary>
    IssuedAdminToken Issue();

    /// <summary>Valida um token sem lançar exceções para entradas malformadas.</summary>
    AdminTokenValidation Validate(string? token);
}

/// <summary>
/// Token: base64url(payloadJson) + "." + base64url(HMACSHA256(signingKey, payloadBytes)), com payload
/// { "iat": unix, "exp": unix, "v": 1 }. signingKey = SHA256("eafc-admin-token-v1|" + Auth:ApiKey), de modo que
/// trocar a senha invalida todos os tokens emitidos. Duração: Auth:TokenHours (padrão 12, limitada a 1..168).
/// </summary>
public sealed class AdminTokenService : IAdminTokenService
{
    public const int DefaultTokenHours = 12;
    public const int MinTokenHours = 1;
    public const int MaxTokenHours = 168;

    private const int CurrentVersion = 1;
    private const int MaxTokenLength = 1024;
    private const string SigningKeyPrefix = "eafc-admin-token-v1|";

    private readonly TimeProvider _time;
    private readonly byte[]? _passwordHash;
    private readonly byte[]? _signingKey;
    private readonly TimeSpan _lifetime;

    public AdminTokenService(IConfiguration config, TimeProvider time)
    {
        _time = time;

        var key = config["Auth:ApiKey"];
        if (!string.IsNullOrEmpty(key))
        {
            _passwordHash = Sha256(key);
            _signingKey = Sha256(SigningKeyPrefix + key);
        }

        var hours = DefaultTokenHours;
        if (int.TryParse(config["Auth:TokenHours"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var configured))
            hours = configured;

        _lifetime = TimeSpan.FromHours(Math.Clamp(hours, MinTokenHours, MaxTokenHours));
    }

    public bool IsConfigured => _passwordHash is not null;

    public bool VerifyPassword(string? candidate)
    {
        if (_passwordHash is null || string.IsNullOrEmpty(candidate))
            return false;

        return CryptographicOperations.FixedTimeEquals(Sha256(candidate), _passwordHash);
    }

    public IssuedAdminToken Issue()
    {
        if (_signingKey is null)
            throw new InvalidOperationException("Auth:ApiKey não configurada.");

        var now = _time.GetUtcNow();
        var expires = now + _lifetime;

        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            iat = now.ToUnixTimeSeconds(),
            exp = expires.ToUnixTimeSeconds(),
            v = CurrentVersion
        });

        var signature = HMACSHA256.HashData(_signingKey, payload);
        var token = Base64UrlEncode(payload) + "." + Base64UrlEncode(signature);

        return new IssuedAdminToken(token, DateTimeOffset.FromUnixTimeSeconds(expires.ToUnixTimeSeconds()));
    }

    public AdminTokenValidation Validate(string? token)
    {
        var invalid = new AdminTokenValidation(AdminTokenStatus.Invalid, null);

        if (_signingKey is null || string.IsNullOrEmpty(token) || token.Length > MaxTokenLength)
            return invalid;

        var dot = token.IndexOf('.');
        if (dot <= 0 || dot != token.LastIndexOf('.') || dot == token.Length - 1)
            return invalid;

        var payload = Base64UrlDecode(token.AsSpan(0, dot));
        var signature = Base64UrlDecode(token.AsSpan(dot + 1));
        if (payload is null || signature is null)
            return invalid;

        var expected = HMACSHA256.HashData(_signingKey, payload);
        if (signature.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(signature, expected))
            return invalid;

        // Assinatura ok: o payload foi emitido por nós. Ainda assim, tratamos defensivamente.
        long exp;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("v", out var v) || !v.TryGetInt32(out var version) || version != CurrentVersion
                || !root.TryGetProperty("iat", out var iat) || !iat.TryGetInt64(out _)
                || !root.TryGetProperty("exp", out var e) || !e.TryGetInt64(out exp))
            {
                return invalid;
            }
        }
        catch (JsonException)
        {
            return invalid;
        }

        DateTimeOffset expiresAt;
        try
        {
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(exp);
        }
        catch (ArgumentOutOfRangeException)
        {
            return invalid;
        }

        return _time.GetUtcNow() >= expiresAt
            ? new AdminTokenValidation(AdminTokenStatus.Expired, expiresAt)
            : new AdminTokenValidation(AdminTokenStatus.Valid, expiresAt);
    }

    private static byte[] Sha256(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // Retorna null para qualquer entrada fora do alfabeto base64url (sem padding) ou com tamanho impossível.
    private static byte[]? Base64UrlDecode(ReadOnlySpan<char> input)
    {
        if (input.IsEmpty || input.Length % 4 == 1)
            return null;

        var padded = new char[input.Length + (4 - input.Length % 4) % 4];
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (c == '-') c = '+';
            else if (c == '_') c = '/';
            else if (!char.IsAsciiLetterOrDigit(c)) return null;
            padded[i] = c;
        }
        for (var i = input.Length; i < padded.Length; i++)
            padded[i] = '=';

        try
        {
            return Convert.FromBase64CharArray(padded, 0, padded.Length);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
