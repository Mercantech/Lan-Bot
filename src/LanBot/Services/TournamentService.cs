using LanBot.Data;
using LanBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanBot;

public sealed class TournamentService
{
    private readonly LanBotDbContext _db;
    private readonly LanEventService _lanEvents;
    private readonly ParticipantService _participants;

    public TournamentService(LanBotDbContext db, LanEventService lanEvents, ParticipantService participants)
    {
        _db = db;
        _lanEvents = lanEvents;
        _participants = participants;
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
            return (false, "PlayersPerMatch skal være mellem 2 og 16.", null);

        if (advanceCount < 1 || advanceCount >= playersPerMatch)
            return (false, "AdvanceCount skal være mindst 1 og mindre end PlayersPerMatch.", null);

        if (progression == TournamentProgression.SingleMatch)
        {
            advanceCount = 1;
        }

        var lan = await _lanEvents.GetOrCreateActiveLanEventAsync(ct);

        var exists = await _db.Tournaments.AnyAsync(x => x.LanEventId == lan.Id && x.Name == name, ct);
        if (exists)
            return (false, "Der findes allerede en turnering med det navn på dette LAN.", null);

        var created = new Tournament
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

        _db.Tournaments.Add(created);
        await _db.SaveChangesAsync(ct);

        return (true, $"Turneringen **{created.Name}** er oprettet (status: Draft).", created);
    }

    public async Task<Tournament?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var lan = await _lanEvents.GetOrCreateActiveLanEventAsync(ct);
        name = name.Trim();
        return await _db.Tournaments.FirstOrDefaultAsync(x => x.LanEventId == lan.Id && x.Name == name, ct);
    }

    public async Task<(bool ok, string message)> SetStatusAsync(string name, TournamentStatus newStatus, CancellationToken ct = default)
    {
        var tournament = await GetByNameAsync(name, ct);
        if (tournament is null)
            return (false, "Jeg kan ikke finde en turnering med det navn.");

        var allowed = IsAllowedTransition(tournament.Status, newStatus);
        if (!allowed)
            return (false, $"Ugyldig status-ændring: {tournament.Status} → {newStatus}.");

        tournament.Status = newStatus;
        await _db.SaveChangesAsync(ct);
        return (true, $"Turneringen **{tournament.Name}** er nu {tournament.Status}.");
    }

    public async Task<(bool ok, string message)> EnrollMeAsync(string tournamentName, ulong discordUserId, CancellationToken ct = default)
    {
        var tournament = await GetByNameAsync(tournamentName, ct);
        if (tournament is null)
            return (false, "Jeg kan ikke finde en turnering med det navn.");

        if (tournament.Status is not (TournamentStatus.Open or TournamentStatus.Draft))
            return (false, $"Turneringen er ikke åben for tilmelding (status: {tournament.Status}).");

        var me = await _participants.GetMeAsync(discordUserId, ct);
        if (me is null)
            return (false, "Du skal først tilmelde dig LAN med `/lan join`.");

        var exists = await _db.TournamentEntries.AnyAsync(x => x.TournamentId == tournament.Id && x.ParticipantId == me.Id, ct);
        if (exists)
            return (false, "Du er allerede tilmeldt den turnering.");

        if (tournament.IsTeamBased)
            return (false, "Denne turnering er hold-baseret. Hold-tilmelding kommer i næste step.");

        _db.TournamentEntries.Add(new TournamentEntry
        {
            TournamentId = tournament.Id,
            ParticipantId = me.Id,
        });
        await _db.SaveChangesAsync(ct);

        return (true, $"Du er nu tilmeldt **{tournament.Name}**.");
    }

    public async Task<(bool ok, string message)> LeaveMeAsync(string tournamentName, ulong discordUserId, CancellationToken ct = default)
    {
        var tournament = await GetByNameAsync(tournamentName, ct);
        if (tournament is null)
            return (false, "Jeg kan ikke finde en turnering med det navn.");

        if (tournament.Status is TournamentStatus.Running or TournamentStatus.Done)
            return (false, $"Du kan ikke afmelde dig når turneringen er startet/afsluttet (status: {tournament.Status}).");

        var me = await _participants.GetMeAsync(discordUserId, ct);
        if (me is null)
            return (false, "Du er ikke LAN-tilmeldt endnu.");

        var entry = await _db.TournamentEntries
            .FirstOrDefaultAsync(x => x.TournamentId == tournament.Id && x.ParticipantId == me.Id, ct);

        if (entry is null)
            return (false, "Du er ikke tilmeldt den turnering.");

        if (tournament.IsTeamBased)
            return (false, "Afmelding for hold-turneringer er ikke understøttet endnu.");

        _db.TournamentEntries.Remove(entry);
        await _db.SaveChangesAsync(ct);

        return (true, $"Du er nu afmeldt **{tournament.Name}**.");
    }

    public async Task<(bool ok, string message)> GetStatusLineAsync(string name, CancellationToken ct = default)
    {
        var tournament = await GetByNameAsync(name, ct);
        if (tournament is null)
            return (false, "Jeg kan ikke finde en turnering med det navn.");

        var entryCount = await _db.TournamentEntries.CountAsync(x => x.TournamentId == tournament.Id, ct);
        var extra = tournament.Status switch
        {
            TournamentStatus.Done when tournament.WinnerParticipantId.HasValue => " (har vinder)",
            TournamentStatus.Done => " (afsluttet)",
            _ => ""
        };

        var progression = tournament.Progression == TournamentProgression.Bracket ? "Bracket" : "Single Match";
        return (true, $"**{tournament.Name}** — {progression} — status: **{tournament.Status}**{extra}, tilmeldte: **{entryCount}**.");
    }

    public async Task<string?> GetCurrentMatchesOverviewAsync(string name, CancellationToken ct = default)
    {
        var tournament = await GetByNameAsync(name, ct);
        if (tournament is null)
            return null;

        var round = await _db.Rounds
            .Where(r => r.TournamentId == tournament.Id)
            .OrderByDescending(r => r.Number)
            .FirstOrDefaultAsync(ct);

        if (round is null)
            return "Der er ingen rounds endnu.";

        var matches = await _db.Matches
            .Where(m => m.RoundId == round.Id)
            .OrderBy(m => m.Seed)
            .ToListAsync(ct);

        if (matches.Count == 0)
            return $"Round {round.Number}: ingen matches.";

        var matchIds = matches.Select(m => m.Id).ToList();
        var slots = await _db.MatchSlots
            .Where(s => matchIds.Contains(s.MatchId))
            .Include(s => s.Participant)
            .ToListAsync(ct);

        var lines = new List<string> { $"**Round {round.Number}**" };
        foreach (var m in matches)
        {
            var matchSlots = slots.Where(s => s.MatchId == m.Id).ToList();
            var players = matchSlots
                .Select(s => s.Participant?.RealName ?? "<?>")
                .ToArray();

            var playerList = players.Length > 0 ? string.Join(" vs ", players) : "(ingen spillere)";
            lines.Add($"- Match {m.Seed}: `{m.Id}` — {m.Status} — {playerList}");
        }

        return string.Join('\n', lines);
    }

    public async Task<(Tournament? tournament, int? roundNumber, List<(int seed, MatchStatus status, List<string> players)> matches)> GetLatestRoundSnapshotAsync(
        string name,
        CancellationToken ct = default)
    {
        var tournament = await GetByNameAsync(name, ct);
        if (tournament is null)
            return (null, null, new());

        var round = await _db.Rounds
            .Where(r => r.TournamentId == tournament.Id)
            .OrderByDescending(r => r.Number)
            .FirstOrDefaultAsync(ct);

        if (round is null)
            return (tournament, null, new());

        var matches = await _db.Matches
            .Where(m => m.RoundId == round.Id)
            .OrderBy(m => m.Seed)
            .ToListAsync(ct);

        var matchIds = matches.Select(m => m.Id).ToList();
        var slots = await _db.MatchSlots
            .Where(s => matchIds.Contains(s.MatchId))
            .Include(s => s.Participant)
            .ToListAsync(ct);

        var result = new List<(int seed, MatchStatus status, List<string> players)>();
        foreach (var m in matches)
        {
            var players = slots
                .Where(s => s.MatchId == m.Id)
                .Select(s => s.Participant?.RealName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .ToList();

            result.Add((m.Seed, m.Status, players));
        }

        return (tournament, round.Number, result);
    }

    public async Task<string?> GetEnrollmentOverviewAsync(string name, CancellationToken ct = default)
    {
        var tournament = await GetByNameAsync(name, ct);
        if (tournament is null)
            return null;

        var entries = await _db.TournamentEntries
            .Where(e => e.TournamentId == tournament.Id)
            .Include(e => e.Participant)
            .OrderBy(e => e.Participant!.RealName)
            .ToListAsync(ct);

        if (entries.Count == 0)
            return "Ingen tilmeldte endnu.";

        var lines = new List<string> { "**Tilmeldte:**" };
        var i = 1;
        foreach (var entry in entries)
        {
            if (entry.Participant is null)
                continue;

            lines.Add($"{i++}. <@{entry.Participant.DiscordUserId}> — {entry.Participant.RealName}");
        }

        if (lines.Count == 1)
            return "Ingen tilmeldte endnu.";

        return string.Join('\n', lines);
    }

    public async Task<string?> GetWinnerLineAsync(string name, CancellationToken ct = default)
    {
        var tournament = await GetByNameAsync(name, ct);
        if (tournament is null)
            return null;

        if (tournament.Status != TournamentStatus.Done)
            return $"Turneringen **{tournament.Name}** er ikke afsluttet endnu (status: {tournament.Status}).";

        if (tournament.IsTeamBased)
        {
            if (tournament.WinnerTeamId is null)
                return $"Turneringen **{tournament.Name}** er afsluttet, men ingen vinder er sat endnu.";

            var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == tournament.WinnerTeamId.Value, ct);
            return team is null
                ? $"Vinder: `{tournament.WinnerTeamId}`"
                : $"Vinder: **{team.Name}**";
        }

        if (tournament.WinnerParticipantId is null)
            return $"Turneringen **{tournament.Name}** er afsluttet, men ingen vinder er sat endnu.";

        var p = await _db.Participants.FirstOrDefaultAsync(x => x.Id == tournament.WinnerParticipantId.Value, ct);
        return p is null
            ? $"Vinder: `{tournament.WinnerParticipantId}`"
            : $"Vinder: **{p.RealName}**";
    }

    private static bool IsAllowedTransition(TournamentStatus from, TournamentStatus to) => (from, to) switch
    {
        (TournamentStatus.Draft, TournamentStatus.Open) => true,
        (TournamentStatus.Draft, TournamentStatus.Closed) => true,
        (TournamentStatus.Open, TournamentStatus.Closed) => true,
        (TournamentStatus.Closed, TournamentStatus.Open) => true,
        (TournamentStatus.Closed, TournamentStatus.Seeded) => true,
        (TournamentStatus.Seeded, TournamentStatus.Running) => true,
        (TournamentStatus.Running, TournamentStatus.Done) => true,
        _ => false
    };
}

