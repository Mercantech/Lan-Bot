using LanBot.Data;
using LanBot.Domain.Brackets;
using LanBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanBot;

public sealed class MatchService
{
    private readonly LanBotDbContext _db;

    public MatchService(LanBotDbContext db)
    {
        _db = db;
    }

    public async Task<(bool ok, string message, List<(ulong discordUserId, string realName)> players)> GetMatchPlayersAsync(Guid matchId, CancellationToken ct = default)
    {
        var slots = await _db.MatchSlots
            .Where(s => s.MatchId == matchId)
            .Include(s => s.Participant)
            .ToListAsync(ct);

        var players = slots
            .Where(s => s.Participant is not null)
            .Select(s => (s.Participant!.DiscordUserId, s.Participant!.RealName))
            .ToList();

        if (players.Count == 0)
            return (false, "Match har ingen spillere.", new());

        return (true, "OK", players);
    }

    public async Task<(bool ok, string message)> ReportSoloMatchAsync(Guid matchId, IReadOnlyList<ulong> orderedDiscordUserIds, CancellationToken ct = default)
    {
        var match = await _db.Matches
            .Include(m => m.Round)
            .ThenInclude(r => r!.Tournament)
            .FirstOrDefaultAsync(m => m.Id == matchId, ct);

        if (match is null || match.Round is null || match.Round.Tournament is null)
            return (false, "Jeg kan ikke finde det match.");

        var tournament = match.Round.Tournament;
        if (tournament.IsTeamBased)
            return (false, "Dette er en hold-turnering. Team match reporting kommer senere.");

        if (match.Status == MatchStatus.Completed)
            return (false, "Det match er allerede rapporteret.");

        var slots = await _db.MatchSlots
            .Where(s => s.MatchId == matchId)
            .Include(s => s.Participant)
            .ToListAsync(ct);

        if (slots.Count == 0)
            return (false, "Match har ingen slots.");

        var expected = slots.Count;
        if (orderedDiscordUserIds.Count != expected)
            return (false, $"Du skal angive præcis {expected} spillere i placeringsrækkefølge.");

        var byDiscordId = slots
            .Where(s => s.Participant is not null)
            .ToDictionary(s => s.Participant!.DiscordUserId, s => s);

        for (var i = 0; i < orderedDiscordUserIds.Count; i++)
        {
            var discordId = orderedDiscordUserIds[i];
            if (!byDiscordId.TryGetValue(discordId, out var slot))
                return (false, "Mindst én bruger matcher ikke en spiller i det match.");

            slot.Placement = i + 1;
        }

        match.Status = MatchStatus.Completed;

        await _db.SaveChangesAsync(ct);

        await AdvanceIfRoundCompleteAsync(match.RoundId, ct);

        return (true, "Match rapporteret.");
    }

    private async Task AdvanceIfRoundCompleteAsync(Guid roundId, CancellationToken ct)
    {
        var round = await _db.Rounds
            .Include(r => r.Tournament)
            .FirstAsync(r => r.Id == roundId, ct);

        var tournament = round.Tournament!;

        var matches = await _db.Matches.Where(m => m.RoundId == roundId).ToListAsync(ct);
        if (matches.Any(m => m.Status != MatchStatus.Completed))
            return;

        var matchIds = matches.Select(m => m.Id).ToList();
        var slots = await _db.MatchSlots
            .Where(s => matchIds.Contains(s.MatchId))
            .ToListAsync(ct);

        var remainingPlayersInRound = slots.Count(s => s.ParticipantId.HasValue);
        var advancementCount = remainingPlayersInRound <= tournament.PlayersPerMatch
            ? 1
            : tournament.AdvanceCount;

        var advancers = slots
            .Where(s => s.Placement.HasValue && s.Placement.Value <= advancementCount)
            .OrderBy(s => s.MatchId)
            .ThenBy(s => s.Placement)
            .Select(s => s.ParticipantId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        if (tournament.Progression == TournamentProgression.SingleMatch)
        {
            var winner = slots
                .Where(s => s.Placement == 1 && s.ParticipantId.HasValue)
                .Select(s => s.ParticipantId!.Value)
                .FirstOrDefault();

            if (winner != Guid.Empty)
            {
                tournament.WinnerParticipantId = winner;
                tournament.Status = TournamentStatus.Done;
                await _db.SaveChangesAsync(ct);
                return;
            }
        }

        if (advancers.Count <= 1)
        {
            tournament.Status = TournamentStatus.Done;
            tournament.WinnerParticipantId = advancers.FirstOrDefault();
            await _db.SaveChangesAsync(ct);
            return;
        }

        var nextRoundNumber = round.Number + 1;
        var nextRound = new Round { TournamentId = tournament.Id, Number = nextRoundNumber };
        _db.Rounds.Add(nextRound);

        var seeded = BracketGenerator.Seed(advancers, tournament.PlayersPerMatch, tournament.RandomizeSeeds);

        var seedIndex = 1;
        foreach (var matchEntrants in seeded)
        {
            var match = new Match { Round = nextRound, Seed = seedIndex++, Status = MatchStatus.Pending };
            _db.Matches.Add(match);

            foreach (var entrant in matchEntrants)
                _db.MatchSlots.Add(new MatchSlot { Match = match, ParticipantId = entrant });
        }

        tournament.Status = TournamentStatus.Running;
        await _db.SaveChangesAsync(ct);
    }
}

