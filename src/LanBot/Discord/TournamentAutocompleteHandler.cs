using Discord;
using Discord.Interactions;
using LanBot.Data;
using LanBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanBot.Discord;

public sealed class TournamentAutocompleteHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction interaction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var db = (LanBotDbContext)services.GetService(typeof(LanBotDbContext))!;

        var userInput = interaction.Data.Current.Value?.ToString() ?? "";

        // "Ikke startet endnu" = alt før Running/Done.
        var query = db.Tournaments
            .Where(t => t.Status != TournamentStatus.Running && t.Status != TournamentStatus.Done);

        if (!string.IsNullOrWhiteSpace(userInput))
            query = query.Where(t => EF.Functions.ILike(t.Name, $"%{userInput}%"));

        var names = await query
            .OrderBy(t => t.Status)
            .ThenBy(t => t.Name)
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

