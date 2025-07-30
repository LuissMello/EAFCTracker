using Microsoft.EntityFrameworkCore;

public class EAFCContext : DbContext
{
    public EAFCContext(DbContextOptions<EAFCContext> options) : base(options) { }

    public DbSet<MatchEntity> Matches { get; set; }
    public DbSet<MatchClubEntity> MatchClubs { get; set; }
    public DbSet<MatchPlayerEntity> MatchPlayers { get; set; }
    public DbSet<PlayerEntity> Players { get; set; }
    public DbSet<PlayerMatchStatsEntity> PlayerMatchStats { get; set; }
    public DbSet<ClubDetailsEntity> ClubDetails { get; set; }
    //public DbSet<CustomKitEntity> CustomKits { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerEntity>()
            .HasKey(m => m.Id);

        modelBuilder.Entity<PlayerEntity>()
            .HasOne(p => p.PlayerMatchStats)
            .WithOne(s => s.Player)
            .HasForeignKey<PlayerMatchStatsEntity>(s => s.PlayerEntityId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MatchClubEntity>()
            .OwnsOne(c => c.Details);

        modelBuilder.Entity<MatchEntity>()
            .HasKey(m => m.MatchId);

        modelBuilder.Entity<MatchEntity>()
            .HasMany(m => m.Clubs)
            .WithOne(c => c.Match)
            .HasForeignKey(c => c.MatchId);

        modelBuilder.Entity<MatchEntity>()
            .HasMany(m => m.MatchPlayers)
            .WithOne(p => p.Match)
            .HasForeignKey(p => p.MatchId);

        modelBuilder.Entity<MatchPlayerEntity>()
            .HasKey(mp => new { mp.MatchId, mp.PlayerEntityId });

        modelBuilder.Entity<MatchPlayerEntity>()
            .HasOne(mp => mp.Match)
            .WithMany(m => m.MatchPlayers)
            .HasForeignKey(mp => mp.MatchId);

        modelBuilder.Entity<MatchPlayerEntity>()
            .HasOne(mp => mp.Player)
            .WithMany(p => p.MatchPlayers)
            .HasForeignKey(mp => mp.PlayerEntityId);

        modelBuilder.Entity<MatchPlayerEntity>()
            .HasOne(mp => mp.PlayerMatchStats)
            .WithMany(p => p.MatchPlayers)
            .HasForeignKey(mp => mp.PlayerMatchStatsEntityId);
    }
}