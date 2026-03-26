using LanBot.Data;
using LanBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LanBot;

public sealed class LanEventService
{
    private readonly BotOptions _options;
    private readonly LanBotDbContext _db;

    public LanEventService(LanBotDbContext db, IOptions<BotOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<LanEvent> GetOrCreateActiveLanEventAsync(CancellationToken ct = default)
    {
        var name = _options.LanEventName.Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = "Default LAN";

        var existing = await _db.LanEvents.FirstOrDefaultAsync(x => x.Name == name, ct);
        if (existing is not null)
            return existing;

        var created = new LanEvent { Name = name };
        _db.LanEvents.Add(created);
        await _db.SaveChangesAsync(ct);
        return created;
    }
}

