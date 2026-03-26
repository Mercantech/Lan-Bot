using Discord;
using Discord.Interactions;
using LanBot.Data;
using Microsoft.EntityFrameworkCore;

namespace LanBot.Discord;

public sealed class MatchPlayerAutocompleteHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction interaction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var db = (LanBotDbContext)services.GetService(typeof(LanBotDbContext))!;

        var matchValue = interaction.Data.Options
            .FirstOrDefault(o => string.Equals(o.Name, "match", StringComparison.OrdinalIgnoreCase))
            ?.Value?.ToString();

        if (!Guid.TryParse(matchValue, out var matchId))
            return AutocompletionResult.FromSuccess(Array.Empty<AutocompleteResult>());

        var userInput = interaction.Data.Current.Value?.ToString() ?? "";

        var players = await db.MatchSlots
            .Where(s => s.MatchId == matchId)
            .Include(s => s.Participant)
            .Where(s => s.Participant != null)
            .Select(s => new { s.Participant!.RealName, s.Participant!.DiscordUserId })
            .ToListAsync();

        var results = players
            .Select(p => new
            {
                Label = $"{p.RealName} (<@{p.DiscordUserId}>)",
                Value = p.DiscordUserId.ToString()
            })
            .Where(x => string.IsNullOrWhiteSpace(userInput) || x.Label.Contains(userInput, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(x => new AutocompleteResult(x.Label, x.Value));

        return AutocompletionResult.FromSuccess(results);
    }
}

