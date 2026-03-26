namespace LanBot.Domain.Entities;

public sealed class TournamentEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TournamentId { get; set; }
    public Tournament? Tournament { get; set; }

    public Guid ParticipantId { get; set; }
    public Participant? Participant { get; set; }

    public Guid? TeamId { get; set; }
    public Team? Team { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

