using Discord;
using LanBot.Data;
using LanBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanBot;

public sealed class TeamService
{
    private readonly LanBotDbContext _db;
    private readonly TournamentService _tournaments;
    private readonly ParticipantService _participants;

    public TeamService(LanBotDbContext db, TournamentService tournaments, ParticipantService participants)
    {
        _db = db;
        _tournaments = tournaments;
        _participants = participants;
    }

    public async Task<(bool ok, string message)> CreateTeamAsync(string tournamentName, string teamName, CancellationToken ct = default)
    {
        var tournament = await _tournaments.GetByNameAsync(tournamentName, ct);
        if (tournament is null)
            return (false, "Jeg kan ikke finde en turnering med det navn.");

        if (!tournament.IsTeamBased)
            return (false, "Denne turnering er ikke hold-baseret.");

        teamName = teamName.Trim();
        if (string.IsNullOrWhiteSpace(teamName))
            return (false, "Du skal give holdet et navn.");

        if (teamName.Length > 200)
            return (false, "Holdnavnet er for langt (max 200 tegn).");

        var exists = await _db.Teams.AnyAsync(x => x.TournamentId == tournament.Id && x.Name == teamName, ct);
        if (exists)
            return (false, "Der findes allerede et hold med det navn i turneringen.");

        _db.Teams.Add(new Team { TournamentId = tournament.Id, Name = teamName });
        await _db.SaveChangesAsync(ct);
        return (true, $"Holdet **{teamName}** er oprettet i **{tournament.Name}**.");
    }

    public async Task<(bool ok, string message)> AddMemberAsync(string tournamentName, string teamName, ulong discordUserId, CancellationToken ct = default)
    {
        var tournament = await _tournaments.GetByNameAsync(tournamentName, ct);
        if (tournament is null)
            return (false, "Jeg kan ikke finde en turnering med det navn.");

        if (!tournament.IsTeamBased)
            return (false, "Denne turnering er ikke hold-baseret.");

        if (tournament.Status is not (TournamentStatus.Draft or TournamentStatus.Open))
            return (false, $"Hold kan ikke ændres nu (status: {tournament.Status}).");

        var participant = await _participants.GetMeAsync(discordUserId, ct);
        if (participant is null)
            return (false, "Brugeren er ikke tilmeldt LAN endnu. Brug `/lan join` først.");

        var team = await _db.Teams.FirstOrDefaultAsync(x => x.TournamentId == tournament.Id && x.Name == teamName, ct);
        if (team is null)
            return (false, "Jeg kan ikke finde et hold med det navn i turneringen.");

        var alreadyOnTeam = await _db.TeamMembers
            .Where(tm => tm.ParticipantId == participant.Id)
            .Join(_db.Teams, tm => tm.TeamId, t => t.Id, (tm, t) => new { tm, t })
            .AnyAsync(x => x.t.TournamentId == tournament.Id, ct);

        if (alreadyOnTeam)
            return (false, "Brugeren er allerede på et hold i den turnering.");

        _db.TeamMembers.Add(new TeamMember { TeamId = team.Id, ParticipantId = participant.Id });

        var entry = await _db.TournamentEntries.FirstOrDefaultAsync(x => x.TournamentId == tournament.Id && x.ParticipantId == participant.Id, ct);
        if (entry is null)
        {
            _db.TournamentEntries.Add(new TournamentEntry
            {
                TournamentId = tournament.Id,
                ParticipantId = participant.Id,
                TeamId = team.Id,
            });
        }
        else
        {
            entry.TeamId = team.Id;
        }

        await _db.SaveChangesAsync(ct);
        return (true, $"Brugeren er tilføjet til **{team.Name}** i **{tournament.Name}**.");
    }
}

