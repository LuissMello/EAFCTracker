using EAFCMatchTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EAFCMatchTracker.Infrastructure.Data;

public class EAFCContext : DbContext
{
    public EAFCContext(DbContextOptions<EAFCContext> options) : base(options) { }

    public DbSet<MatchEntity> Matches { get; set; }
    public DbSet<MatchClubEntity> MatchClubs { get; set; }
    public DbSet<MatchPlayerEntity> MatchPlayers { get; set; }
    public DbSet<PlayerEntity> Players { get; set; }
    public DbSet<PlayerMatchStatsEntity> PlayerMatchStats { get; set; }
    public DbSet<OverallStatsEntity> OverallStats { get; set; }
    public DbSet<PlayoffAchievementEntity> PlayoffAchievements { get; set; }
    public DbSet<SystemFetchAudit> SystemFetchAudits { get; set; }
    public DbSet<MatchGoalLinkEntity> MatchGoalLinks { get; set; }
    public DbSet<AppSettingEntity> AppSettings { get; set; }
    public DbSet<TrackedClubEntity> TrackedClubs { get; set; }
    public DbSet<SessionBoundaryEntity> SessionBoundaries { get; set; }
    public DbSet<GameVersionEntity> GameVersions { get; set; }
    public DbSet<GoalRegistrationEntity> GoalRegistrations { get; set; }
    public DbSet<GoalRegistrationGoalEntity> GoalRegistrationGoals { get; set; }
    public DbSet<PlayerArchetypeEntity> PlayerArchetypes { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MatchEntity>()
            .HasKey(m => m.MatchId);

        modelBuilder.Entity<MatchEntity>()
            .HasMany(m => m.Clubs)
            .WithOne(c => c.Match)
            .HasForeignKey(c => c.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MatchEntity>()
            .HasMany(m => m.MatchPlayers)
            .WithOne(mp => mp.Match)
            .HasForeignKey(mp => mp.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MatchClubEntity>()
            .HasKey(mc => mc.Id);

        modelBuilder.Entity<MatchClubEntity>()
            .HasIndex(mc => new { mc.MatchId, mc.ClubId })
            .IsUnique();

        // Busca de partidas por clube (Where ClubId = X / join com Matches)
        modelBuilder.Entity<MatchClubEntity>()
            .HasIndex(mc => new { mc.ClubId, mc.MatchId });

        modelBuilder.Entity<MatchEntity>()
            .HasIndex(m => m.Timestamp);

        modelBuilder.Entity<MatchClubEntity>()
            .OwnsOne(mc => mc.Details, cb =>
            {
                cb.WithOwner();
            });

        modelBuilder.Entity<MatchClubEntity>()
            .Property(mc => mc.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<PlayerEntity>()
            .HasKey(p => p.Id);

        modelBuilder.Entity<PlayerEntity>()
            .HasIndex(p => new { p.PlayerId, p.ClubId })
            .IsUnique();

        modelBuilder.Entity<PlayerEntity>()
            .HasOne(p => p.PlayerMatchStats)
            .WithMany()
            .HasForeignKey(p => p.PlayerMatchStatsId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PlayerMatchStatsEntity>()
            .HasKey(s => s.Id);

        modelBuilder.Entity<PlayerMatchStatsEntity>()
            .HasOne(s => s.Player)
            .WithMany()
            .HasForeignKey(s => s.PlayerEntityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PlayerMatchStatsEntity>()
            .HasIndex(s => s.PlayerEntityId)
            .IsUnique(false);

        modelBuilder.Entity<MatchPlayerEntity>()
            .HasKey(mp => new { mp.MatchId, mp.ClubId, mp.PlayerEntityId });

        modelBuilder.Entity<MatchPlayerEntity>()
            .HasOne(mp => mp.Player)
            .WithMany(p => p.MatchPlayers)
            .HasForeignKey(mp => mp.PlayerEntityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MatchPlayerEntity>()
            .HasOne(mp => mp.PlayerMatchStats)
            .WithMany(s => s.MatchPlayers)
            .HasForeignKey(mp => mp.PlayerMatchStatsEntityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<OverallStatsEntity>()
            .HasKey(o => o.Id);

        modelBuilder.Entity<OverallStatsEntity>()
            .HasIndex(o => o.ClubId);

        // "Último overall do clube" (ORDER BY UpdatedAtUtc DESC)
        modelBuilder.Entity<OverallStatsEntity>()
            .HasIndex(o => new { o.ClubId, o.UpdatedAtUtc });

        modelBuilder.Entity<OverallStatsEntity>()
            .Property(o => o.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<PlayoffAchievementEntity>()
            .HasKey(p => p.Id);

        modelBuilder.Entity<PlayoffAchievementEntity>()
            .Property(p => p.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<PlayoffAchievementEntity>()
            .HasIndex(p => new { p.ClubId, p.SeasonId })
            .IsUnique();

        modelBuilder.Entity<PlayoffAchievementEntity>()
            .HasIndex(p => p.ClubId);


        // OnModelCreating(...)
        modelBuilder.Entity<SystemFetchAudit>()
        .HasKey(x => x.Id);

        modelBuilder.Entity<SystemFetchAudit>()
        .Property(x => x.Id)
        .ValueGeneratedNever();

        // opcional: semear 1 linha inicial para evitar null
        modelBuilder.Entity<SystemFetchAudit>()
        .HasData(new SystemFetchAudit { Id = 1, LastFetchedAt = DateTimeOffset.MinValue
         });

        // ===============================
        // MatchGoalLinkEntity
        // ===============================
        modelBuilder.Entity<MatchGoalLinkEntity>()
            .HasKey(g => g.Id);

        modelBuilder.Entity<MatchGoalLinkEntity>()
            .Property(g => g.Id)
            .ValueGeneratedOnAdd();

        // FK → Match
        modelBuilder.Entity<MatchGoalLinkEntity>()
            .HasOne(g => g.Match)
            .WithMany()
            .HasForeignKey(g => g.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK → Scorer (obrigatório)
        modelBuilder.Entity<MatchGoalLinkEntity>()
            .HasOne(g => g.Scorer)
            .WithMany()
            .HasForeignKey(g => g.ScorerPlayerEntityId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK → Assist (opcional)
        modelBuilder.Entity<MatchGoalLinkEntity>()
            .HasOne(g => g.Assist)
            .WithMany()
            .HasForeignKey(g => g.AssistPlayerEntityId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK → Pre-Assist (opcional)
        modelBuilder.Entity<MatchGoalLinkEntity>()
            .HasOne(g => g.PreAssist)
            .WithMany()
            .HasForeignKey(g => g.PreAssistPlayerEntityId)
            .OnDelete(DeleteBehavior.Restrict);

        // Índice para buscas rápidas por partida
        modelBuilder.Entity<MatchGoalLinkEntity>()
            .HasIndex(g => new { g.MatchId, g.ClubId });

        // ===============================
        // GameVersions (edições do jogo: FC25, FC26, FC27...)
        // ===============================
        modelBuilder.Entity<GameVersionEntity>(gv =>
        {
            gv.HasKey(v => v.Id);
            gv.Property(v => v.Id).ValueGeneratedNever(); // Id atribuído pelo repositório (max + 1)
            gv.Property(v => v.Name).IsRequired().HasMaxLength(50);
            gv.HasIndex(v => v.Version).IsUnique();

            // No máximo UMA edição corrente
            gv.HasIndex(v => v.IsCurrent)
              .IsUnique()
              .HasFilter("\"IsCurrent\" = true");

            gv.HasData(
                new GameVersionEntity { Id = 1, Version = 25, Name = "FC25", StartsAt = new DateTimeOffset(2024, 9, 27, 0, 0, 0, TimeSpan.Zero), IsCurrent = false },
                new GameVersionEntity { Id = 2, Version = 26, Name = "FC26", StartsAt = new DateTimeOffset(2025, 9, 26, 0, 0, 0, TimeSpan.Zero), IsCurrent = false },
                new GameVersionEntity { Id = 3, Version = 27, Name = "FC27", StartsAt = null, IsCurrent = true });
        });

        // FK opcional (Restrict) Match/OverallStats/PlayoffAchievements/TrackedClubs -> GameVersions (sem navegação)
        modelBuilder.Entity<MatchEntity>()
            .HasOne<GameVersionEntity>().WithMany()
            .HasForeignKey(m => m.GameVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MatchEntity>().HasIndex(m => m.GameVersionId);

        modelBuilder.Entity<OverallStatsEntity>()
            .HasOne<GameVersionEntity>().WithMany()
            .HasForeignKey(o => o.GameVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OverallStatsEntity>().HasIndex(o => o.GameVersionId);

        modelBuilder.Entity<PlayoffAchievementEntity>()
            .HasOne<GameVersionEntity>().WithMany()
            .HasForeignKey(p => p.GameVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TrackedClubEntity>()
            .HasOne<GameVersionEntity>().WithMany()
            .HasForeignKey(c => c.GameVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        // ===============================
        // GoalRegistrations (registro antecipado de gols vinculado depois à partida)
        // ===============================
        modelBuilder.Entity<GoalRegistrationEntity>(r =>
        {
            r.HasKey(x => x.Id);
            r.Property(x => x.Id).ValueGeneratedOnAdd();
            r.Property(x => x.OpponentName).IsRequired().HasMaxLength(100);
            r.Property(x => x.Notes).HasMaxLength(500);
            r.Property(x => x.ReviewNote).HasMaxLength(500);
            r.Property(x => x.Status).HasConversion<int>();

            r.HasIndex(x => new { x.ClubId, x.OpponentClubId, x.Status });
            r.HasIndex(x => new { x.Status, x.CreatedAt });
            r.HasIndex(x => x.MatchId)
             .IsUnique()
             .HasFilter("\"MatchId\" IS NOT NULL");

            // Partida apagada -> registro volta a ficar sem MatchId (o linker o devolve a Pending)
            r.HasOne<MatchEntity>().WithMany()
             .HasForeignKey(x => x.MatchId)
             .OnDelete(DeleteBehavior.SetNull);

            r.HasMany(x => x.Goals)
             .WithOne(g => g.GoalRegistration)
             .HasForeignKey(g => g.GoalRegistrationId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GoalRegistrationGoalEntity>(g =>
        {
            g.HasKey(x => x.Id);
            g.Property(x => x.Id).ValueGeneratedOnAdd();
            g.HasIndex(x => new { x.GoalRegistrationId, x.Order });

            g.HasOne(x => x.Scorer).WithMany()
             .HasForeignKey(x => x.ScorerPlayerEntityId).OnDelete(DeleteBehavior.Restrict);
            g.HasOne(x => x.Assist).WithMany()
             .HasForeignKey(x => x.AssistPlayerEntityId).OnDelete(DeleteBehavior.Restrict);
            g.HasOne(x => x.PreAssist).WithMany()
             .HasForeignKey(x => x.PreAssistPlayerEntityId).OnDelete(DeleteBehavior.Restrict);
        });

        // Matches.GoalRegistrationId e MatchGoalLinks.GoalRegistrationId: referência de volta ao registro
        modelBuilder.Entity<MatchEntity>(m =>
        {
            m.HasOne<GoalRegistrationEntity>().WithMany()
             .HasForeignKey(x => x.GoalRegistrationId)
             .OnDelete(DeleteBehavior.SetNull);
            m.HasIndex(x => x.GoalRegistrationId)
             .IsUnique()
             .HasFilter("\"GoalRegistrationId\" IS NOT NULL");
        });

        modelBuilder.Entity<MatchGoalLinkEntity>(l =>
        {
            l.HasOne<GoalRegistrationEntity>().WithMany()
             .HasForeignKey(x => x.GoalRegistrationId)
             .OnDelete(DeleteBehavior.SetNull);
            l.HasIndex(x => x.GoalRegistrationId);
        });

        // Catálogo de arquétipos (id da EA -> nome/sigla/grupo editáveis no Admin). Tabela puramente aditiva.
        modelBuilder.Entity<PlayerArchetypeEntity>(a =>
        {
            a.HasKey(x => x.Id);
            a.Property(x => x.Id).ValueGeneratedNever();
            a.Property(x => x.Name).HasMaxLength(PlayerArchetypeEntity.MaxNameLength);
            a.Property(x => x.ShortName).HasMaxLength(PlayerArchetypeEntity.MaxShortNameLength);
            a.Property(x => x.PositionGroup).HasMaxLength(PlayerArchetypeEntity.MaxPositionGroupLength);
        });

        // AppSettings (chave-valor de configuração)
        modelBuilder.Entity<AppSettingEntity>()
            .HasKey(s => s.Key);

        modelBuilder.Entity<AppSettingEntity>()
            .HasData(
                new AppSettingEntity { Key = AppSettingEntity.Keys.FetchIntervalMinutes, Value = "60" },
                new AppSettingEntity { Key = AppSettingEntity.Keys.MaxParallelFetches, Value = "4" }
            );

        // TrackedClubs (clubes rastreados)
        modelBuilder.Entity<TrackedClubEntity>()
            .HasKey(c => c.ClubId);

        modelBuilder.Entity<TrackedClubEntity>()
            .Property(c => c.ClubId)
            .ValueGeneratedNever();

        modelBuilder.Entity<TrackedClubEntity>()
            .Property(c => c.TimeZoneId).HasMaxLength(100).HasDefaultValue("America/Sao_Paulo");
        modelBuilder.Entity<TrackedClubEntity>()
            .Property(c => c.SessionGapMinutes).HasDefaultValue(120);

        modelBuilder.Entity<SessionBoundaryEntity>()
            .HasKey(b => new { b.ClubId, b.MatchId });
        modelBuilder.Entity<SessionBoundaryEntity>()
            .HasOne<TrackedClubEntity>().WithMany().HasForeignKey(b => b.ClubId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<SessionBoundaryEntity>()
            .HasOne<MatchEntity>().WithMany().HasForeignKey(b => b.MatchId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TrackedClubEntity>()
            .HasData(
                new TrackedClubEntity { ClubId = 355651, Name = null, AddedAt = DateTimeOffset.MinValue },
                new TrackedClubEntity { ClubId = 352016, Name = null, AddedAt = DateTimeOffset.MinValue },
                new TrackedClubEntity { ClubId = 349613, Name = null, AddedAt = DateTimeOffset.MinValue },
                new TrackedClubEntity { ClubId = 312721, Name = null, AddedAt = DateTimeOffset.MinValue }
            );
    }
}
