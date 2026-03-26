using LanBot.Data;
using LanBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanBot.Web.Services;

public sealed class TournamentReadService
{
    private readonly LanBotDbContext _db;

    public TournamentReadService(LanBotDbContext db)
    {
        _db = db;
    }

    public async Task<List<TournamentListItem>> GetTournamentsAsync(CancellationToken ct = default)
    {
        var items = await _db.Tournaments
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TournamentListItem(
                t.Id,
                t.Name,
                t.Status,
                t.Progression,
                t.IsTeamBased,
                t.PlayersPerMatch,
                t.AdvanceCount,
                _db.TournamentEntries.Count(e => e.TournamentId == t.Id)))
            .ToListAsync(ct);

        return items;
    }

    public async Task<TournamentDetailsDto?> GetTournamentDetailsAsync(Guid tournamentId, CancellationToken ct = default)
    {
        var tournament = await _db.Tournaments
            .FirstOrDefaultAsync(t => t.Id == tournamentId, ct);

        if (tournament is null)
            return null;

        var entries = await _db.TournamentEntries
            .Where(e => e.TournamentId == tournament.Id)
            .Include(e => e.Participant)
            .OrderBy(e => e.Participant!.RealName)
            .ToListAsync(ct);

        var participants = entries
            .Where(e => e.Participant is not null)
            .Select(e => new ParticipantDto(e.Participant!.DiscordUserId, e.Participant!.RealName))
            .ToList();

        var rounds = await _db.Rounds
            .Where(r => r.TournamentId == tournament.Id)
            .OrderBy(r => r.Number)
            .ToListAsync(ct);

        var roundIds = rounds.Select(r => r.Id).ToList();
        var matches = await _db.Matches
            .Where(m => roundIds.Contains(m.RoundId))
            .OrderBy(m => m.RoundId)
            .ThenBy(m => m.Seed)
            .ToListAsync(ct);

        var matchIds = matches.Select(m => m.Id).ToList();
        var slots = await _db.MatchSlots
            .Where(s => matchIds.Contains(s.MatchId))
            .Include(s => s.Participant)
            .ToListAsync(ct);

        var roundDtos = new List<RoundDto>();
        foreach (var round in rounds)
        {
            var roundMatches = matches.Where(m => m.RoundId == round.Id).ToList();
            var matchDtos = new List<MatchDto>();

            foreach (var match in roundMatches)
            {
                var players = slots
                    .Where(s => s.MatchId == match.Id)
                    .OrderBy(s => s.Placement ?? int.MaxValue)
                    .Select(s => new MatchPlayerDto(
                        s.Participant?.RealName ?? "<?>",
                        s.Participant?.DiscordUserId,
                        s.Placement))
                    .ToList();

                matchDtos.Add(new MatchDto(match.Seed, match.Status, players));
            }

            roundDtos.Add(new RoundDto(round.Number, matchDtos));
        }

        string? winnerName = null;
        if (tournament.WinnerParticipantId.HasValue)
        {
            winnerName = await _db.Participants
                .Where(p => p.Id == tournament.WinnerParticipantId.Value)
                .Select(p => p.RealName)
                .FirstOrDefaultAsync(ct);
        }

        return new TournamentDetailsDto(
            tournament.Id,
            tournament.Name,
            tournament.Status,
            tournament.Progression,
            tournament.IsTeamBased,
            tournament.PlayersPerMatch,
            tournament.AdvanceCount,
            participants,
            roundDtos,
            winnerName);
    }

    public async Task<List<ScoreboardItem>> GetScoreboardAsync(CancellationToken ct = default)
    {
        var done = await _db.Tournaments
            .Where(t => t.Status == TournamentStatus.Done)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        var winnerIds = done
            .Where(t => t.WinnerParticipantId.HasValue)
            .Select(t => t.WinnerParticipantId!.Value)
            .Distinct()
            .ToList();

        var winners = await _db.Participants
            .Where(p => winnerIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.RealName, ct);

        return done.Select(t =>
        {
            var winner = t.WinnerParticipantId.HasValue && winners.TryGetValue(t.WinnerParticipantId.Value, out var name)
                ? name
                : "Ukendt";
            return new ScoreboardItem(t.Id, t.Name, winner, t.Progression, t.CreatedAt);
        }).ToList();
    }
}

public sealed record TournamentListItem(
    Guid Id,
    string Name,
    TournamentStatus Status,
    TournamentProgression Progression,
    bool IsTeamBased,
    int PlayersPerMatch,
    int AdvanceCount,
    int EntryCount);

public sealed record ParticipantDto(ulong DiscordUserId, string RealName);
public sealed record MatchPlayerDto(string Name, ulong? DiscordUserId, int? Placement);
public sealed record MatchDto(int Seed, MatchStatus Status, List<MatchPlayerDto> Players);
public sealed record RoundDto(int Number, List<MatchDto> Matches);

public sealed record TournamentDetailsDto(
    Guid Id,
    string Name,
    TournamentStatus Status,
    TournamentProgression Progression,
    bool IsTeamBased,
    int PlayersPerMatch,
    int AdvanceCount,
    List<ParticipantDto> Participants,
    List<RoundDto> Rounds,
    string? WinnerName);

public sealed record ScoreboardItem(
    Guid TournamentId,
    string TournamentName,
    string WinnerName,
    TournamentProgression Progression,
    DateTimeOffset CompletedAt);

