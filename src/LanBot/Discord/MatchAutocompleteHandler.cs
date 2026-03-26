using Discord;
using Discord.Interactions;
using LanBot.Data;
using LanBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanBot.Discord;

public sealed class MatchAutocompleteHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction interaction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var db = (LanBotDbContext)services.GetService(typeof(LanBotDbContext))!;

        var tournamentName = interaction.Data.Options
            .FirstOrDefault(o => string.Equals(o.Name, "turnering", StringComparison.OrdinalIgnoreCase))
            ?.Value?.ToString();

        if (string.IsNullOrWhiteSpace(tournamentName))
            return AutocompletionResult.FromSuccess(Array.Empty<AutocompleteResult>());

        var tournament = await db.Tournaments.FirstOrDefaultAsync(t => t.Name == tournamentName);
        if (tournament is null)
            return AutocompletionResult.FromSuccess(Array.Empty<AutocompleteResult>());

        var round = await db.Rounds
            .Where(r => r.TournamentId == tournament.Id)
            .OrderByDescending(r => r.Number)
            .FirstOrDefaultAsync();

        if (round is null)
            return AutocompletionResult.FromSuccess(Array.Empty<AutocompleteResult>());

        var matches = await db.Matches
            .Where(m => m.RoundId == round.Id && m.Status == MatchStatus.Pending)
            .OrderBy(m => m.Seed)
            .ToListAsync();

        if (matches.Count == 0)
            return AutocompletionResult.FromSuccess(Array.Empty<AutocompleteResult>());

        var matchIds = matches.Select(m => m.Id).ToList();
        var slots = await db.MatchSlots
            .Where(s => matchIds.Contains(s.MatchId))
            .Include(s => s.Participant)
            .ToListAsync();

        var userInput = interaction.Data.Current.Value?.ToString() ?? "";

        var results = new List<AutocompleteResult>();
        foreach (var m in matches)
        {
            var players = slots
                .Where(s => s.MatchId == m.Id)
                .Select(s => s.Participant?.RealName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .ToList();

            var shortId = m.Id.ToString("N")[..8];
            var label = $"Match {m.Seed} — {string.Join(" • ", players)} — {shortId}";
            if (!string.IsNullOrWhiteSpace(userInput) && !label.Contains(userInput, StringComparison.OrdinalIgnoreCase))
                continue;

            results.Add(new AutocompleteResult(label, m.Id.ToString()));
            if (results.Count >= 25) break;
        }

        return AutocompletionResult.FromSuccess(results);
    }
}

