namespace EAFCMatchTracker.Domain.Entities;

public class AppSettingEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    public static class Keys
    {
        public const string FetchIntervalMinutes = "fetch_interval_minutes";
        public const string MaxParallelFetches = "max_parallel_fetches";
        public const string LiveIntervalMinutes = "live_interval_minutes";

        /// <summary>Até quantos minutos DEPOIS do início do registro de gols (createdAt) a partida pode aparecer para ser vinculada. A tolerância para partidas ligeiramente anteriores é GoalRegistration:LinkSlackMinutes (padrão 15).</summary>
        public const string GoalLinkWindowMinutes = "goal_link_window_minutes";

        /// <summary>Dias até um registro de gols sem partida correspondente expirar.</summary>
        public const string GoalRegistrationExpireDays = "goal_registration_expire_days";

        /// <summary>"true" enquanto o modo ao vivo estiver ligado (sem expiração). Persistido em runtime, não editável via admin.</summary>
        public const string LiveEnabled = "live_enabled";
    }

    /// <summary>Configurações inteiras editáveis (whitelist) com valor padrão e faixa válida.</summary>
    public static class Definitions
    {
        public sealed record IntSetting(string Key, int Default, int Min, int Max);

        public static readonly IReadOnlyList<IntSetting> All = new[]
        {
            new IntSetting(Keys.FetchIntervalMinutes, 60, 1, 1440),
            new IntSetting(Keys.MaxParallelFetches, 4, 1, 8),
            new IntSetting(Keys.LiveIntervalMinutes, 5, 1, 60),
            new IntSetting(Keys.GoalLinkWindowMinutes, 360, 30, 2880),
            new IntSetting(Keys.GoalRegistrationExpireDays, 7, 1, 60),
        };

        public static IntSetting? Find(string key) =>
            All.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal));
    }
}
