namespace LanBot.Domain.Entities;

public sealed class MatchSlot
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MatchId { get; set; }
    public Match? Match { get; set; }

    public Guid? ParticipantId { get; set; }
    public Participant? Participant { get; set; }

    public Guid? TeamId { get; set; }
    public Team? Team { get; set; }

    public int? Placement { get; set; }
}

