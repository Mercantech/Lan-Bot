namespace LanBot.Domain.Entities;

public sealed class TournamentAnnouncement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TournamentId { get; set; }
    public Tournament? Tournament { get; set; }

    public ulong ChannelId { get; set; }
    public ulong MessageId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

