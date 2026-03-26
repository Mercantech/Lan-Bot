namespace LanBot.Domain.Entities;

public sealed class Round
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TournamentId { get; set; }
    public Tournament? Tournament { get; set; }

    public int Number { get; set; }
}

