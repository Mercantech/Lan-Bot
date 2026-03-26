namespace LanBot.Domain.Entities;

public sealed class Participant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LanEventId { get; set; }
    public LanEvent? LanEvent { get; set; }

    public ulong DiscordUserId { get; set; }

    public string RealName { get; set; } = "";
    public string RealNameNormalized { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

