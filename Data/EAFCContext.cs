using Microsoft.EntityFrameworkCore;

public class EAFCContext : DbContext
{
    public EAFCContext(DbContextOptions<EAFCContext> options) : base(options) { }

    public DbSet<MatchEntity> Matches { get; set; }
    public DbSet<MatchClubEntity> MatchClubs { get; set; }
    public DbSet<MatchPlayerEntity> MatchPlayers { get; set; }
    public DbSet<PlayerEntity> Players { get; set; }
    public DbSet<PlayerMatchStatsEntity> PlayerMatchStats { get; set; }
    public DbSet<OverallStatsEntity> OverallStats { get; set; }

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
            .HasIndex(o => o.ClubId)
            .IsUnique();

        modelBuilder.Entity<OverallStatsEntity>()
            .Property(o => o.Id)
            .ValueGeneratedOnAdd();
    }
}
