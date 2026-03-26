namespace LanBot.Domain.Entities;

public sealed class Match
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RoundId { get; set; }
    public Round? Round { get; set; }

    public int Seed { get; set; }

    public MatchStatus Status { get; set; } = MatchStatus.Pending;
}

