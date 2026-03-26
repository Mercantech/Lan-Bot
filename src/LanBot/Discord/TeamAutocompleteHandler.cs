using Discord;
using Discord.Interactions;
using LanBot.Data;
using Microsoft.EntityFrameworkCore;

namespace LanBot.Discord;

public sealed class TeamAutocompleteHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction interaction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var db = (LanBotDbContext)services.GetService(typeof(LanBotDbContext))!;
        var userInput = interaction.Data.Current.Value?.ToString() ?? "";
        var tournamentName = interaction.Data.Options
            .FirstOrDefault(o => string.Equals(o.Name, "turnering", StringComparison.OrdinalIgnoreCase))
            ?.Value?.ToString();

        if (string.IsNullOrWhiteSpace(tournamentName))
            return AutocompletionResult.FromSuccess(Array.Empty<AutocompleteResult>());

        var tournamentId = await db.Tournaments
            .Where(t => t.Name == tournamentName)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync();

        if (!tournamentId.HasValue)
            return AutocompletionResult.FromSuccess(Array.Empty<AutocompleteResult>());

        var query = db.Teams.Where(t => t.TournamentId == tournamentId.Value);
        if (!string.IsNullOrWhiteSpace(userInput))
            query = query.Where(t => EF.Functions.ILike(t.Name, $"%{userInput}%"));

        var names = await query
            .OrderBy(t => t.Name)
            .Select(t => t.Name)
            .Take(25)
            .ToListAsync();

        var results = names
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(25)
            .Select(n => new AutocompleteResult(n, n));

        return AutocompletionResult.FromSuccess(results);
    }
}

