namespace LanBot.Domain.Entities;

public sealed class Team
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TournamentId { get; set; }
    public Tournament? Tournament { get; set; }

    public string Name { get; set; } = "";
}

