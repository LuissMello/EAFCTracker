namespace EAFCMatchTracker.Api.Security;

/// <summary>Controle em memória de tentativas de login falhas por cliente (IP), com bloqueio temporário.</summary>
public interface ILoginAttemptTracker
{
    /// <summary>True (e o tempo restante) se o cliente estiver bloqueado.</summary>
    bool IsLockedOut(string clientId, out TimeSpan retryAfter);

    /// <summary>Registra uma falha. Retorna true se o cliente ficou (ou já estava) bloqueado após ela.</summary>
    bool RegisterFailure(string clientId);

    /// <summary>Zera o contador do cliente (login bem-sucedido).</summary>
    void Reset(string clientId);
}

/// <summary>
/// 5 falhas dentro de 15 minutos bloqueiam o cliente por 15 minutos. Memória limitada: entradas antigas são
/// purgadas periodicamente e há um teto de entradas rastreadas.
/// </summary>
public sealed class LoginAttemptTracker : ILoginAttemptTracker
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private const int MaxEntries = 10_000;
    private static readonly TimeSpan PurgeInterval = TimeSpan.FromMinutes(1);

    private sealed class Entry
    {
        public int Failures;
        public DateTimeOffset WindowStart;
        public DateTimeOffset? LockedUntil;
    }

    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private DateTimeOffset _lastPurge;

    public LoginAttemptTracker(TimeProvider time)
    {
        _time = time;
        _lastPurge = time.GetUtcNow();
    }

    public bool IsLockedOut(string clientId, out TimeSpan retryAfter)
    {
        lock (_gate)
        {
            var now = _time.GetUtcNow();
            if (_entries.TryGetValue(clientId, out var entry) && entry.LockedUntil is { } until && until > now)
            {
                retryAfter = until - now;
                return true;
            }
        }

        retryAfter = TimeSpan.Zero;
        return false;
    }

    public bool RegisterFailure(string clientId)
    {
        lock (_gate)
        {
            var now = _time.GetUtcNow();
            PurgeIfNeeded(now);

            if (!_entries.TryGetValue(clientId, out var entry))
            {
                if (_entries.Count >= MaxEntries && !EvictOldestUnlocked(now))
                    return false; // tudo bloqueado e sem espaço: não rastreia este cliente

                entry = new Entry { WindowStart = now };
                _entries[clientId] = entry;
            }
            else if (entry.LockedUntil is { } locked && locked > now)
            {
                return true;
            }
            else if (entry.LockedUntil is not null || now - entry.WindowStart >= Window)
            {
                // Bloqueio ou janela anterior expiraram: recomeça a contagem
                entry.Failures = 0;
                entry.WindowStart = now;
                entry.LockedUntil = null;
            }

            entry.Failures++;
            if (entry.Failures >= MaxFailures)
            {
                entry.LockedUntil = now + LockoutDuration;
                return true;
            }

            return false;
        }
    }

    public void Reset(string clientId)
    {
        lock (_gate)
        {
            _entries.Remove(clientId);
        }
    }

    private void PurgeIfNeeded(DateTimeOffset now)
    {
        if (now - _lastPurge < PurgeInterval && _entries.Count < MaxEntries)
            return;

        _lastPurge = now;
        List<string>? stale = null;
        foreach (var (key, entry) in _entries)
        {
            var lockActive = entry.LockedUntil is { } until && until > now;
            if (!lockActive && now - entry.WindowStart >= Window)
                (stale ??= []).Add(key);
        }

        if (stale is null) return;
        foreach (var key in stale)
            _entries.Remove(key);
    }

    private bool EvictOldestUnlocked(DateTimeOffset now)
    {
        string? oldestKey = null;
        var oldest = DateTimeOffset.MaxValue;
        foreach (var (key, entry) in _entries)
        {
            if (entry.LockedUntil is { } until && until > now) continue;
            if (entry.WindowStart < oldest)
            {
                oldest = entry.WindowStart;
                oldestKey = key;
            }
        }

        if (oldestKey is null) return false;
        _entries.Remove(oldestKey);
        return true;
    }
}
