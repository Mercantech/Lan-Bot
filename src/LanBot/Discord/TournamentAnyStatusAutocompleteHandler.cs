using Discord;
using Discord.Interactions;
using LanBot.Data;
using Microsoft.EntityFrameworkCore;

namespace LanBot.Discord;

public sealed class TournamentAnyStatusAutocompleteHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction interaction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var db = (LanBotDbContext)services.GetService(typeof(LanBotDbContext))!;
        var userInput = interaction.Data.Current.Value?.ToString() ?? "";

        var query = db.Tournaments.AsQueryable();
        if (!string.IsNullOrWhiteSpace(userInput))
            query = query.Where(t => EF.Functions.ILike(t.Name, $"%{userInput}%"));

        var names = await query
            .OrderByDescending(t => t.CreatedAt)
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

