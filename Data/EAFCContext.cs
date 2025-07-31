using Microsoft.EntityFrameworkCore;

public class EAFCContext : DbContext
{
    public EAFCContext(DbContextOptions<EAFCContext> options) : base(options) { }

    public DbSet<MatchEntity> Matches { get; set; }
    public DbSet<MatchClubEntity> MatchClubs { get; set; }
    public DbSet<MatchPlayerEntity> MatchPlayers { get; set; }
    public DbSet<PlayerEntity> Players { get; set; }
    public DbSet<PlayerMatchStatsEntity> PlayerMatchStats { get; set; }

    // ⚠️ ClubDetailsEntity é uma owned type — não precisa (e não deve) ser um DbSet
    // public DbSet<ClubDetailsEntity> ClubDetails { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Match
        modelBuilder.Entity<MatchEntity>()
            .HasKey(m => m.MatchId);

        modelBuilder.Entity<MatchEntity>()
            .HasMany(m => m.Clubs)
            .WithOne(c => c.Match)
            .HasForeignKey(c => c.MatchId);

        modelBuilder.Entity<MatchEntity>()
            .HasMany(m => m.MatchPlayers)
            .WithOne(mp => mp.Match)
            .HasForeignKey(mp => mp.MatchId);

        // MatchClub + ClubDetails
        modelBuilder.Entity<MatchClubEntity>()
            .HasKey(mc => new { mc.MatchId, mc.ClubId });

        modelBuilder.Entity<MatchClubEntity>()
            .OwnsOne(mc => mc.Details, cb =>
            {
                cb.WithOwner(); // obrigatório para owned type
                // opcional: cb.Property(d => d.Name).HasColumnName("ClubName"); etc.
            });

        // Player
        modelBuilder.Entity<PlayerEntity>()
            .HasKey(p => p.Id);

        modelBuilder.Entity<PlayerEntity>()
            .HasIndex(p => new { p.PlayerId, p.ClubId }) // garante unicidade lógica
            .IsUnique();

        modelBuilder.Entity<PlayerEntity>()
            .HasOne(p => p.PlayerMatchStats)
            .WithOne(s => s.Player)
            .HasForeignKey<PlayerMatchStatsEntity>(s => s.PlayerEntityId)
            .OnDelete(DeleteBehavior.Cascade);

        // MatchPlayer
        modelBuilder.Entity<MatchPlayerEntity>()
            .HasKey(mp => new { mp.MatchId, mp.PlayerEntityId });

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
