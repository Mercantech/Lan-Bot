using LanBot.Data;
using LanBot.Domain;
using LanBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanBot;

public sealed class ParticipantService
{
    private readonly LanBotDbContext _db;
    private readonly LanEventService _lanEvents;

    public ParticipantService(LanBotDbContext db, LanEventService lanEvents)
    {
        _db = db;
        _lanEvents = lanEvents;
    }

    public async Task<Participant?> GetMeAsync(ulong discordUserId, CancellationToken ct = default)
    {
        var lan = await _lanEvents.GetOrCreateActiveLanEventAsync(ct);
        return await _db.Participants.FirstOrDefaultAsync(x => x.LanEventId == lan.Id && x.DiscordUserId == discordUserId, ct);
    }

    public async Task<(bool ok, string message, Participant? participant)> RegisterOrUpdateAsync(
        ulong discordUserId,
        string realName,
        CancellationToken ct = default)
    {
        var lan = await _lanEvents.GetOrCreateActiveLanEventAsync(ct);

        var normalized = NameNormalization.Normalize(realName);
        if (string.IsNullOrWhiteSpace(normalized))
            return (false, "Du skal skrive dit rigtige navn.", null);

        if (realName.Trim().Length > 200)
            return (false, "Navnet er for langt (max 200 tegn).", null);

        var nameTaken = await _db.Participants.AnyAsync(
            x => x.LanEventId == lan.Id && x.RealNameNormalized == normalized && x.DiscordUserId != discordUserId,
            ct);

        if (nameTaken)
            return (false, "Det navn er allerede i brug til dette LAN.", null);

        var existing = await _db.Participants.FirstOrDefaultAsync(
            x => x.LanEventId == lan.Id && x.DiscordUserId == discordUserId,
            ct);

        if (existing is null)
        {
            var created = new Participant
            {
                LanEventId = lan.Id,
                DiscordUserId = discordUserId,
                RealName = realName.Trim(),
                RealNameNormalized = normalized,
            };
            _db.Participants.Add(created);
            await _db.SaveChangesAsync(ct);
            return (true, $"Du er nu tilmeldt LAN som **{created.RealName}**.", created);
        }

        existing.RealName = realName.Trim();
        existing.RealNameNormalized = normalized;
        await _db.SaveChangesAsync(ct);
        return (true, $"Dit navn er opdateret til **{existing.RealName}**.", existing);
    }
}

