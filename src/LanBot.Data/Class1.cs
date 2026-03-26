using LanBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanBot.Data;

public sealed class LanBotDbContext : DbContext
{
    public LanBotDbContext(DbContextOptions<LanBotDbContext> options) : base(options)
    {
    }

    public DbSet<LanEvent> LanEvents => Set<LanEvent>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<TournamentEntry> TournamentEntries => Set<TournamentEntry>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<Round> Rounds => Set<Round>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchSlot> MatchSlots => Set<MatchSlot>();
    public DbSet<TournamentAnnouncement> TournamentAnnouncements => Set<TournamentAnnouncement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<LanEvent>(b =>
        {
            b.ToTable("lan_events");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<Participant>(b =>
        {
            b.ToTable("participants");
            b.HasKey(x => x.Id);

            b.HasOne(x => x.LanEvent)
                .WithMany()
                .HasForeignKey(x => x.LanEventId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Property(x => x.DiscordUserId).IsRequired();
            b.Property(x => x.RealName).HasMaxLength(200).IsRequired();
            b.Property(x => x.RealNameNormalized).HasMaxLength(220).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();

            b.HasIndex(x => new { x.LanEventId, x.DiscordUserId }).IsUnique();
            b.HasIndex(x => new { x.LanEventId, x.RealNameNormalized }).IsUnique();
        });

        modelBuilder.Entity<Tournament>(b =>
        {
            b.ToTable("tournaments");
            b.HasKey(x => x.Id);

            b.HasOne(x => x.LanEvent)
                .WithMany()
                .HasForeignKey(x => x.LanEventId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.Progression).IsRequired();
            b.Property(x => x.IsTeamBased).IsRequired();
            b.Property(x => x.RandomizeSeeds).IsRequired();
            b.Property(x => x.PlayersPerMatch).IsRequired();
            b.Property(x => x.AdvanceCount).IsRequired();
            b.Property(x => x.WinnerParticipantId);
            b.Property(x => x.WinnerTeamId);
            b.Property(x => x.CreatedAt).IsRequired();

            b.HasIndex(x => new { x.LanEventId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<TournamentEntry>(b =>
        {
            b.ToTable("tournament_entries");
            b.HasKey(x => x.Id);

            b.HasOne(x => x.Tournament)
                .WithMany()
                .HasForeignKey(x => x.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Participant)
                .WithMany()
                .HasForeignKey(x => x.ParticipantId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Team)
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.SetNull);

            b.Property(x => x.CreatedAt).IsRequired();

            b.HasIndex(x => new { x.TournamentId, x.ParticipantId }).IsUnique();
        });

        modelBuilder.Entity<Team>(b =>
        {
            b.ToTable("teams");
            b.HasKey(x => x.Id);

            b.HasOne(x => x.Tournament)
                .WithMany()
                .HasForeignKey(x => x.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.HasIndex(x => new { x.TournamentId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<TeamMember>(b =>
        {
            b.ToTable("team_members");
            b.HasKey(x => new { x.TeamId, x.ParticipantId });

            b.HasOne(x => x.Team)
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Participant)
                .WithMany()
                .HasForeignKey(x => x.ParticipantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Round>(b =>
        {
            b.ToTable("rounds");
            b.HasKey(x => x.Id);

            b.HasOne(x => x.Tournament)
                .WithMany()
                .HasForeignKey(x => x.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Property(x => x.Number).IsRequired();
            b.HasIndex(x => new { x.TournamentId, x.Number }).IsUnique();
        });

        modelBuilder.Entity<Match>(b =>
        {
            b.ToTable("matches");
            b.HasKey(x => x.Id);

            b.HasOne(x => x.Round)
                .WithMany()
                .HasForeignKey(x => x.RoundId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Property(x => x.Seed).IsRequired();
            b.Property(x => x.Status).IsRequired();
            b.HasIndex(x => new { x.RoundId, x.Seed }).IsUnique();
        });

        modelBuilder.Entity<MatchSlot>(b =>
        {
            b.ToTable("match_slots");
            b.HasKey(x => x.Id);

            b.HasOne(x => x.Match)
                .WithMany()
                .HasForeignKey(x => x.MatchId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Participant)
                .WithMany()
                .HasForeignKey(x => x.ParticipantId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(x => x.Team)
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.SetNull);

            b.Property(x => x.Placement);
            b.HasIndex(x => new { x.MatchId, x.ParticipantId });
            b.HasIndex(x => new { x.MatchId, x.TeamId });
        });

        modelBuilder.Entity<TournamentAnnouncement>(b =>
        {
            b.ToTable("tournament_announcements");
            b.HasKey(x => x.Id);

            b.HasOne(x => x.Tournament)
                .WithMany()
                .HasForeignKey(x => x.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Property(x => x.ChannelId).IsRequired();
            b.Property(x => x.MessageId).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();

            b.HasIndex(x => x.TournamentId).IsUnique();
            b.HasIndex(x => new { x.ChannelId, x.MessageId }).IsUnique();
        });
    }
}
