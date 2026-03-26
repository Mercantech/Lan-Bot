namespace LanBot.Domain.Entities;

public sealed class Tournament
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LanEventId { get; set; }
    public LanEvent? LanEvent { get; set; }

    public string Name { get; set; } = "";
    public TournamentStatus Status { get; set; } = TournamentStatus.Draft;
    public TournamentProgression Progression { get; set; } = TournamentProgression.Bracket;

    public bool IsTeamBased { get; set; }
    public bool RandomizeSeeds { get; set; } = true;
    public int PlayersPerMatch { get; set; } = 4;
    public int AdvanceCount { get; set; } = 2;

    public Guid? WinnerParticipantId { get; set; }
    public Guid? WinnerTeamId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

