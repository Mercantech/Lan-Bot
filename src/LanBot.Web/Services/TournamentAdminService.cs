using LanBot.Data;
using LanBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanBot.Web.Services;

public sealed class TournamentAdminService
{
    private readonly LanBotDbContext _db;
    private readonly IConfiguration _configuration;

    public TournamentAdminService(LanBotDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<(bool ok, string message, Tournament? tournament)> CreateAsync(
        string name,
        TournamentProgression progression,
        bool isTeamBased,
        bool randomizeSeeds,
        int playersPerMatch,
        int advanceCount,
        CancellationToken ct = default)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return (false, "Du skal give turneringen et navn.", null);

        if (name.Length > 200)
            return (false, "Turneringsnavnet er for langt (max 200 tegn).", null);

        if (playersPerMatch < 2 || playersPerMatch > 16)
            return (false, "Match-stoerrelse skal vaere mellem 2 og 16.", null);

        if (advanceCount < 1 || advanceCount >= playersPerMatch)
            return (false, "Antal videre skal vaere mindst 1 og mindre end match-stoerrelse.", null);

        if (progression == TournamentProgression.SingleMatch)
        {
            advanceCount = 1;
        }

        var lan = await GetOrCreateLanEventAsync(ct);
        var exists = await _db.Tournaments.AnyAsync(x => x.LanEventId == lan.Id && x.Name == name, ct);
        if (exists)
            return (false, "Der findes allerede en turnering med det navn paa dette LAN.", null);

        var tournament = new Tournament
        {
            LanEventId = lan.Id,
            Name = name,
            Progression = progression,
            IsTeamBased = isTeamBased,
            RandomizeSeeds = randomizeSeeds,
            PlayersPerMatch = playersPerMatch,
            AdvanceCount = advanceCount,
            Status = TournamentStatus.Draft,
        };

        _db.Tournaments.Add(tournament);
        await _db.SaveChangesAsync(ct);
        return (true, $"Turneringen **{tournament.Name}** er oprettet.", tournament);
    }

    private async Task<LanEvent> GetOrCreateLanEventAsync(CancellationToken ct)
    {
        var lanName =
            Environment.GetEnvironmentVariable("LAN_EVENT_NAME")
            ?? _configuration["LAN_EVENT_NAME"]
            ?? "Default LAN";

        var existing = await _db.LanEvents
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.Name == lanName, ct);

        if (existing is not null)
            return existing;

        var lan = new LanEvent { Name = lanName.Trim() };
        _db.LanEvents.Add(lan);
        await _db.SaveChangesAsync(ct);
        return lan;
    }
}
