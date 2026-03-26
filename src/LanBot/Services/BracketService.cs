using LanBot.Data;
using LanBot.Domain.Brackets;
using LanBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanBot;

public sealed class BracketService
{
    private readonly LanBotDbContext _db;
    private readonly TournamentService _tournaments;

    public BracketService(LanBotDbContext db, TournamentService tournaments)
    {
        _db = db;
        _tournaments = tournaments;
    }

    public async Task<(bool ok, string message)> SeedAsync(string tournamentName, CancellationToken ct = default)
    {
        var tournament = await _tournaments.GetByNameAsync(tournamentName, ct);
        if (tournament is null)
            return (false, "Jeg kan ikke finde en turnering med det navn.");

        if (tournament.Status is TournamentStatus.Seeded or TournamentStatus.Running or TournamentStatus.Done)
            return (false, $"Turneringen er allerede seeded/kører/afsluttet (status: {tournament.Status}).");

        var entries = await _db.TournamentEntries
            .Where(x => x.TournamentId == tournament.Id)
            .ToListAsync(ct);

        if (entries.Count < 2)
            return (false, $"Der er for få tilmeldte (tilmeldte: {entries.Count}).");

        if (tournament.Progression == TournamentProgression.SingleMatch && entries.Count > tournament.PlayersPerMatch)
            return (false, $"Single Match tillader max {tournament.PlayersPerMatch} tilmeldte. Der er {entries.Count}.");

        if (tournament.Progression == TournamentProgression.Bracket && entries.Count < tournament.PlayersPerMatch)
            return (false, $"Der er for få tilmeldte til at lave brackets (tilmeldte: {entries.Count}).");

        if (tournament.IsTeamBased)
        {
            var teamIds = entries
                .Where(x => x.TeamId.HasValue)
                .Select(x => x.TeamId!.Value)
                .Distinct()
                .ToList();

            if (teamIds.Count < 2)
                return (false, "Der er for få hold med medlemmer til at lave brackets.");

            await CreateRound1TeamsAsync(tournament, teamIds, ct);
        }
        else
        {
            var participantIds = entries.Select(x => x.ParticipantId).ToList();
            await CreateRound1ParticipantsAsync(tournament, participantIds, ct);
        }

        tournament.Status = TournamentStatus.Seeded;
        await _db.SaveChangesAsync(ct);

        return (true, $"Brackets er genereret for **{tournament.Name}** (Round 1).");
    }

    private async Task CreateRound1ParticipantsAsync(Tournament tournament, IReadOnlyList<Guid> participantIds, CancellationToken ct)
    {
        var existingRounds = await _db.Rounds.AnyAsync(x => x.TournamentId == tournament.Id, ct);
        if (existingRounds)
            throw new InvalidOperationException("Tournament already has rounds; reseeding is not supported yet.");

        var seeded = BracketGenerator.Seed(participantIds, tournament.PlayersPerMatch, tournament.RandomizeSeeds);

        var round = new Round { TournamentId = tournament.Id, Number = 1 };
        _db.Rounds.Add(round);

        var seedIndex = 1;
        foreach (var matchEntrants in seeded)
        {
            var match = new Match { Round = round, Seed = seedIndex++, Status = MatchStatus.Pending };
            _db.Matches.Add(match);

            foreach (var entrant in matchEntrants)
            {
                var slot = new MatchSlot { Match = match };
                slot.ParticipantId = entrant;

                _db.MatchSlots.Add(slot);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task CreateRound1TeamsAsync(Tournament tournament, IReadOnlyList<Guid> teamIds, CancellationToken ct)
    {
        var existingRounds = await _db.Rounds.AnyAsync(x => x.TournamentId == tournament.Id, ct);
        if (existingRounds)
            throw new InvalidOperationException("Tournament already has rounds; reseeding is not supported yet.");

        var seeded = BracketGenerator.Seed(teamIds, tournament.PlayersPerMatch, tournament.RandomizeSeeds);

        var round = new Round { TournamentId = tournament.Id, Number = 1 };
        _db.Rounds.Add(round);

        var seedIndex = 1;
        foreach (var matchEntrants in seeded)
        {
            var match = new Match { Round = round, Seed = seedIndex++, Status = MatchStatus.Pending };
            _db.Matches.Add(match);

            foreach (var entrant in matchEntrants)
            {
                var slot = new MatchSlot { Match = match, TeamId = entrant };
                _db.MatchSlots.Add(slot);
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}

