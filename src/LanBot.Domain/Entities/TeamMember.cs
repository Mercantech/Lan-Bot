namespace LanBot.Domain.Entities;

public sealed class TeamMember
{
    public Guid TeamId { get; set; }
    public Team? Team { get; set; }

    public Guid ParticipantId { get; set; }
    public Participant? Participant { get; set; }
}

