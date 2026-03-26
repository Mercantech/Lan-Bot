using Discord.Interactions;
using LanBot.Discord;
using Discord.WebSocket;
using Discord;

namespace LanBot;

[Group("match", "Match-kommandoer")]
public sealed class MatchModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly MatchService _matches;
    private readonly AuditLogService _audit;
    private readonly AdminService _admin;
    private readonly MatchReportUiState _uiState;
    private readonly TournamentService _tournaments;

    public MatchModule(MatchService matches, AuditLogService audit, AdminService admin, MatchReportUiState uiState, TournamentService tournaments)
    {
        _matches = matches;
        _audit = audit;
        _admin = admin;
        _uiState = uiState;
        _tournaments = tournaments;
    }

    [SlashCommand("report", "Rapportér resultat for et match (admin)")]
    public async Task ReportAsync(
        [Summary(description: "Turneringens navn")]
        [Autocomplete(typeof(TournamentAutocompleteHandler))]
        string turnering,
        [Summary(description: "Match (fra seneste round)")]
        [Autocomplete(typeof(MatchAutocompleteHandler))]
        string match,
        [Summary(description: "1. plads")]
        [Autocomplete(typeof(MatchPlayerAutocompleteHandler))]
        string first,
        [Summary(description: "2. plads")]
        [Autocomplete(typeof(MatchPlayerAutocompleteHandler))]
        string second,
        [Summary(description: "3. plads")]
        [Autocomplete(typeof(MatchPlayerAutocompleteHandler))]
        string? third = null,
        [Summary(description: "4. plads")]
        [Autocomplete(typeof(MatchPlayerAutocompleteHandler))]
        string? fourth = null)
    {
        await DeferAsync(ephemeral: true);
        if (Context.User is not SocketGuildUser gu || !_admin.IsAdmin(gu))
        {
            await FollowupAsync("Du har ikke rettigheder til den kommando.", ephemeral: true);
            return;
        }

        if (!Guid.TryParse(match, out var id))
        {
            await FollowupAsync("Ugyldigt match valg.", ephemeral: true);
            return;
        }

        if (!ulong.TryParse(first, out var firstId) || !ulong.TryParse(second, out var secondId))
        {
            await FollowupAsync("Ugyldigt spiller-valg.", ephemeral: true);
            return;
        }

        var ordered = new List<ulong> { firstId, secondId };
        if (!string.IsNullOrWhiteSpace(third))
        {
            if (!ulong.TryParse(third, out var thirdId)) { await FollowupAsync("Ugyldigt 3. plads valg.", ephemeral: true); return; }
            ordered.Add(thirdId);
        }
        if (!string.IsNullOrWhiteSpace(fourth))
        {
            if (!ulong.TryParse(fourth, out var fourthId)) { await FollowupAsync("Ugyldigt 4. plads valg.", ephemeral: true); return; }
            ordered.Add(fourthId);
        }

        var (ok, message) = await _matches.ReportSoloMatchAsync(id, ordered);
        if (ok) await _audit.TryLogAsync($"[match.report] {Context.User} rapporterede `{id}` ({turnering}): {string.Join(", ", ordered.Select(x => $"<@{x}>"))}");
        await FollowupAsync(message, ephemeral: true);
    }

    [SlashCommand("report-ui", "Rapportér resultat via dropdowns (admin)")]
    public async Task ReportUiAsync(
        [Summary(description: "Turneringens navn")]
        [Autocomplete(typeof(TournamentAutocompleteHandler))]
        string turnering,
        [Summary(description: "Match (fra seneste round)")]
        [Autocomplete(typeof(MatchAutocompleteHandler))]
        string match)
    {
        await DeferAsync(ephemeral: true);
        if (Context.User is not SocketGuildUser gu || !_admin.IsAdmin(gu))
        {
            await FollowupAsync("Du har ikke rettigheder til den kommando.", ephemeral: true);
            return;
        }

        if (!Guid.TryParse(match, out var matchId))
        {
            await FollowupAsync("Ugyldigt match valg.", ephemeral: true);
            return;
        }

        var (ok, _, players) = await _matches.GetMatchPlayersAsync(matchId);
        if (!ok)
        {
            await FollowupAsync("Jeg kan ikke finde spillere for det match.", ephemeral: true);
            return;
        }

        var playerIds = players.Select(p => p.discordUserId).ToList();
        var options = players
            .Select(p => new SelectMenuOptionBuilder(p.realName, p.discordUserId.ToString()))
            .ToList();

        var menu1 = new SelectMenuBuilder()
            .WithCustomId($"mr:{matchId}:p1")
            .WithPlaceholder("1. plads")
            .WithMinValues(1).WithMaxValues(1);
        foreach (var o in options) menu1.AddOption(o);

        var menu2 = new SelectMenuBuilder()
            .WithCustomId($"mr:{matchId}:p2")
            .WithPlaceholder("2. plads")
            .WithMinValues(1).WithMaxValues(1);
        foreach (var o in options) menu2.AddOption(o);

        var menu3 = new SelectMenuBuilder()
            .WithCustomId($"mr:{matchId}:p3")
            .WithPlaceholder("3. plads")
            .WithMinValues(1).WithMaxValues(1);
        foreach (var o in options) menu3.AddOption(o);

        var menu4 = new SelectMenuBuilder()
            .WithCustomId($"mr:{matchId}:p4")
            .WithPlaceholder("4. plads")
            .WithMinValues(1).WithMaxValues(1);
        foreach (var o in options) menu4.AddOption(o);

        var embed = new EmbedBuilder()
            .WithTitle($"Match report — {turnering}")
            .WithColor(new Color(0x34, 0x98, 0xDB))
            .WithDescription("Vælg placeringer (kun spillere fra match'et). Tryk **Gem** når du er færdig.")
            .AddField("Spillere", string.Join('\n', players.Select(p => $"<@{p.discordUserId}> — {p.realName}")), inline: false)
            .Build();

        var components = new ComponentBuilder()
            .WithSelectMenu(menu1)
            .WithSelectMenu(menu2)
            .WithSelectMenu(menu3)
            .WithSelectMenu(menu4)
            .WithButton("Gem", $"mr:{matchId}:submit", ButtonStyle.Success)
            .WithButton("Annullér", $"mr:{matchId}:cancel", ButtonStyle.Danger)
            .Build();

        var msg = await FollowupAsync(embed: embed, components: components, ephemeral: true);
        _uiState.Create(msg.Id, matchId, playerIds);
    }

    [ComponentInteraction("mr:*:*")]
    public async Task HandleMatchReportUiAsync(string matchId, string action)
    {
        if (Context.Interaction is not SocketMessageComponent component)
            return;

        if (!Guid.TryParse(matchId, out var id))
            return;

        if (!_uiState.TryGet(component.Message.Id, out var draft))
        {
            await component.RespondAsync("Denne menu er udløbet. Kør `/match report-ui` igen.", ephemeral: true);
            return;
        }

        if (action is "p1" or "p2" or "p3" or "p4")
        {
            var value = component.Data.Values.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(value) || !ulong.TryParse(value, out var selected))
            {
                await component.RespondAsync("Ugyldigt valg.", ephemeral: true);
                return;
            }

            if (!draft.PlayerIds.Contains(selected))
            {
                await component.RespondAsync("Du kan kun vælge spillere fra match'et.", ephemeral: true);
                return;
            }

            switch (action)
            {
                case "p1": draft.First = selected; break;
                case "p2": draft.Second = selected; break;
                case "p3": draft.Third = selected; break;
                case "p4": draft.Fourth = selected; break;
            }

            await component.DeferAsync(ephemeral: true);
            return;
        }

        if (action == "cancel")
        {
            _uiState.Remove(component.Message.Id);
            await component.UpdateAsync(m =>
            {
                m.Components = new ComponentBuilder().Build();
                m.Content = "Annulleret.";
                m.Embeds = Array.Empty<Embed>();
            });
            return;
        }

        if (action == "submit")
        {
            if (draft.First is null || draft.Second is null)
            {
                await component.RespondAsync("Du skal mindst vælge 1. og 2. plads.", ephemeral: true);
                return;
            }

            var ordered = new List<ulong> { draft.First.Value, draft.Second.Value };
            if (draft.Third is not null) ordered.Add(draft.Third.Value);
            if (draft.Fourth is not null) ordered.Add(draft.Fourth.Value);

            if (ordered.Distinct().Count() != ordered.Count)
            {
                await component.RespondAsync("Du kan ikke vælge den samme spiller flere gange.", ephemeral: true);
                return;
            }

            var (ok, message) = await _matches.ReportSoloMatchAsync(draft.MatchId, ordered);
            if (ok) await _audit.TryLogAsync($"[match.report.ui] {Context.User} rapporterede `{draft.MatchId}`: {string.Join(", ", ordered.Select(x => $"<@{x}>"))}");

            _uiState.Remove(component.Message.Id);
            await component.UpdateAsync(m =>
            {
                m.Components = new ComponentBuilder().Build();
                m.Content = message;
                m.Embeds = Array.Empty<Embed>();
            });
        }
    }
}

