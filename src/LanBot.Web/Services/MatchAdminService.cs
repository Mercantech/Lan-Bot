using LanBot.Data;
using LanBot.Domain.Brackets;
using LanBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanBot.Web.Services;

public sealed class MatchAdminService
{
    private readonly LanBotDbContext _db;

    public MatchAdminService(LanBotDbContext db)
    {
        _db = db;
    }

    public async Task<List<AdminTournamentItem>> GetTournamentsAsync(CancellationToken ct = default)
    {
        return await _db.Tournaments
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new AdminTournamentItem(t.Id, t.Name, t.Status))
            .ToListAsync(ct);
    }

    public async Task<List<AdminMatchItem>> GetReportableMatchesAsync(Guid tournamentId, CancellationToken ct = default)
    {
        return await _db.Matches
            .Where(m => m.Round!.TournamentId == tournamentId && m.Status != MatchStatus.Completed)
            .OrderByDescending(m => m.Round!.Number)
            .ThenBy(m => m.Seed)
            .Select(m => new AdminMatchItem(
                m.Id,
                m.Round!.Number,
                m.Seed,
                m.Status))
            .ToListAsync(ct);
    }

    public async Task<List<AdminMatchPlayerItem>> GetMatchPlayersAsync(Guid matchId, CancellationToken ct = default)
    {
        return await _db.MatchSlots
            .Where(s => s.MatchId == matchId && s.ParticipantId.HasValue)
            .Include(s => s.Participant)
            .OrderBy(s => s.Placement ?? int.MaxValue)
            .ThenBy(s => s.Participant!.RealName)
            .Select(s => new AdminMatchPlayerItem(
                s.ParticipantId!.Value,
                s.Participant!.RealName,
                s.Participant!.DiscordUserId,
                s.Placement))
            .ToListAsync(ct);
    }

    public async Task<(bool ok, string message)> ReportSoloMatchAsync(Guid matchId, IReadOnlyList<Guid> orderedParticipantIds, CancellationToken ct = default)
    {
        var match = await _db.Matches
            .Include(m => m.Round)
            .ThenInclude(r => r!.Tournament)
            .FirstOrDefaultAsync(m => m.Id == matchId, ct);

        if (match is null || match.Round is null || match.Round.Tournament is null)
            return (false, "Kan ikke finde det match.");

        if (match.Round.Tournament.IsTeamBased)
            return (false, "Hold-baseret match rapportering er ikke understoettet endnu.");

        if (match.Status == MatchStatus.Completed)
            return (false, "Match er allerede rapporteret.");

        var slots = await _db.MatchSlots
            .Where(s => s.MatchId == matchId)
            .ToListAsync(ct);

        if (slots.Count == 0)
            return (false, "Match har ingen spillere.");

        if (orderedParticipantIds.Count != slots.Count)
            return (false, $"Du skal rangere praecis {slots.Count} spillere.");

        var slotByParticipant = slots
            .Where(s => s.ParticipantId.HasValue)
            .ToDictionary(s => s.ParticipantId!.Value, s => s);

        for (var i = 0; i < orderedParticipantIds.Count; i++)
        {
            var participantId = orderedParticipantIds[i];
            if (!slotByParticipant.TryGetValue(participantId, out var slot))
                return (false, "Mindst en spiller findes ikke i match'et.");

            slot.Placement = i + 1;
        }

        match.Status = MatchStatus.Completed;
        await _db.SaveChangesAsync(ct);

        await AdvanceIfRoundCompleteAsync(match.RoundId, ct);
        return (true, "Match er nu afgjort og gemt.");
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

        var nextRound = new Round
        {
            TournamentId = tournament.Id,
            Number = round.Number + 1
        };
        _db.Rounds.Add(nextRound);

        var seeded = BracketGenerator.Seed(advancers, tournament.PlayersPerMatch, tournament.RandomizeSeeds);
        var seed = 1;
        foreach (var entrants in seeded)
        {
            var nextMatch = new Match
            {
                Round = nextRound,
                Seed = seed++,
                Status = MatchStatus.Pending
            };
            _db.Matches.Add(nextMatch);

            foreach (var entrant in entrants)
            {
                _db.MatchSlots.Add(new MatchSlot
                {
                    Match = nextMatch,
                    ParticipantId = entrant
                });
            }
        }

        tournament.Status = TournamentStatus.Running;
        await _db.SaveChangesAsync(ct);
    }
}

public sealed record AdminTournamentItem(Guid Id, string Name, TournamentStatus Status);
public sealed record AdminMatchItem(Guid Id, int RoundNumber, int Seed, MatchStatus Status);
public sealed record AdminMatchPlayerItem(Guid ParticipantId, string RealName, ulong DiscordUserId, int? Placement);
