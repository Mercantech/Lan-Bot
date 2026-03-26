using Discord.Interactions;
using LanBot.Domain.Entities;
using LanBot.Discord;
using Discord.WebSocket;
using Discord;

namespace LanBot;

[Group("tournament", "Turnerings-kommandoer")]
public sealed class TournamentModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly TournamentService _tournaments;
    private readonly BracketService _brackets;
    private readonly AuditLogService _audit;
    private readonly AdminService _admin;
    private readonly TournamentAnnouncementService _announcements;
    private readonly TournamentCreateFlowState _createFlow;

    public TournamentModule(
        TournamentService tournaments,
        BracketService brackets,
        AuditLogService audit,
        AdminService admin,
        TournamentAnnouncementService announcements,
        TournamentCreateFlowState createFlow)
    {
        _tournaments = tournaments;
        _brackets = brackets;
        _audit = audit;
        _admin = admin;
        _announcements = announcements;
        _createFlow = createFlow;
    }

    [SlashCommand("create", "Opret en turnering (admin)")]
    public async Task CreateAsync(
        [Summary(description: "Navn på turneringen")] string navn,
        [Summary(description: "Solo eller hold")] TournamentType type = TournamentType.Solo)
    {
        await DeferAsync(ephemeral: true);
        if (Context.User is not SocketGuildUser gu || !_admin.IsAdmin(gu))
        {
            await FollowupAsync("Du har ikke rettigheder til den kommando.", ephemeral: true);
            return;
        }

        var embed = BuildCreateFlowEmbed(new TournamentCreateFlowState.Draft
        {
            Name = navn,
            IsTeamBased = type == TournamentType.Team,
            Progression = TournamentProgression.Bracket,
            RandomizeSeeds = true,
            PlayersPerMatch = 4,
            AdvanceCount = 2
        });
        var components = BuildCreateFlowComponents();
        var msg = await FollowupAsync(embed: embed, components: components, ephemeral: true);
        var draft = _createFlow.Create(msg.Id, navn);
        draft.IsTeamBased = type == TournamentType.Team;
    }

    [ComponentInteraction("tcf_prog_bracket")]
    public Task SetProgressionBracketAsync() => UpdateDraftAsync(d => d.Progression = TournamentProgression.Bracket);

    [ComponentInteraction("tcf_prog_single")]
    public Task SetProgressionSingleAsync() => UpdateDraftAsync(d =>
    {
        d.Progression = TournamentProgression.SingleMatch;
        d.AdvanceCount = 1;
    });

    [ComponentInteraction("tcf_type_solo")]
    public Task SetTypeSoloAsync() => UpdateDraftAsync(d => d.IsTeamBased = false);

    [ComponentInteraction("tcf_type_team")]
    public Task SetTypeTeamAsync() => UpdateDraftAsync(d => d.IsTeamBased = true);

    [ComponentInteraction("tcf_rand_on")]
    public Task SetRandomOnAsync() => UpdateDraftAsync(d => d.RandomizeSeeds = true);

    [ComponentInteraction("tcf_rand_off")]
    public Task SetRandomOffAsync() => UpdateDraftAsync(d => d.RandomizeSeeds = false);

    [ComponentInteraction("tcf_ppm")]
    public Task SetPlayersPerMatchAsync()
    {
        return UpdateDraftAsync(d =>
        {
            if (Context.Interaction is SocketMessageComponent cmp)
            {
                var v = cmp.Data.Values.FirstOrDefault();
                if (int.TryParse(v, out var parsed))
                {
                    d.PlayersPerMatch = parsed;
                    if (d.AdvanceCount >= d.PlayersPerMatch)
                        d.AdvanceCount = Math.Max(1, d.PlayersPerMatch - 1);
                }
            }
        });
    }

    [ComponentInteraction("tcf_adv")]
    public Task SetAdvanceCountAsync()
    {
        return UpdateDraftAsync(d =>
        {
            if (d.Progression == TournamentProgression.SingleMatch)
            {
                d.AdvanceCount = 1;
                return;
            }

            if (Context.Interaction is SocketMessageComponent cmp)
            {
                var v = cmp.Data.Values.FirstOrDefault();
                if (int.TryParse(v, out var parsed))
                    d.AdvanceCount = parsed;
            }
        });
    }

    [ComponentInteraction("tcf_cancel")]
    public async Task CancelCreateFlowAsync()
    {
        if (Context.Interaction is not SocketMessageComponent component)
            return;

        try
        {
            _createFlow.Remove(component.Message.Id);
            await component.UpdateAsync(m =>
            {
                m.Content = "Turnering-oprettelse annulleret.";
                m.Embeds = Array.Empty<Embed>();
                m.Components = new ComponentBuilder().Build();
            });
        }
        catch
        {
            try { await component.RespondAsync("Kunne ikke annullere. Prøv igen.", ephemeral: true); } catch { }
        }
    }

    [ComponentInteraction("tcf_confirm")]
    public async Task ConfirmCreateFlowAsync()
    {
        if (Context.Interaction is not SocketMessageComponent component)
            return;
        if (Context.User is not SocketGuildUser gu || !_admin.IsAdmin(gu))
        {
            await component.RespondAsync("Du har ikke rettigheder til den kommando.", ephemeral: true);
            return;
        }

        if (!_createFlow.TryGet(component.Message.Id, out var d))
        {
            await component.RespondAsync("Flow udløbet. Kør `/tournament create` igen.", ephemeral: true);
            return;
        }

        if (d.Progression == TournamentProgression.SingleMatch)
            d.AdvanceCount = 1;

        string? message;
        try
        {
            var (ok, createdMessage, tournament) = await _tournaments.CreateAsync(
                d.Name,
                progression: d.Progression,
                isTeamBased: d.IsTeamBased,
                randomizeSeeds: d.RandomizeSeeds,
                playersPerMatch: d.PlayersPerMatch,
                advanceCount: d.AdvanceCount);

            message = createdMessage;

            if (ok && tournament is not null)
            {
                await _announcements.TryAnnounceTournamentAsync(tournament, Context.User, Context.Channel.Id);
                await _audit.TryLogAsync($"[tournament.create] {Context.User} oprettede **{d.Name}** (prog: {d.Progression}, team: {d.IsTeamBased}, random: {d.RandomizeSeeds}, ppm: {d.PlayersPerMatch}, adv: {d.AdvanceCount})");
            }
        }
        catch
        {
            try { await component.RespondAsync("Kunne ikke oprette turneringen. Prøv igen.", ephemeral: true); } catch { }
            return;
        }

        try
        {
            _createFlow.Remove(component.Message.Id);
            await component.UpdateAsync(m =>
            {
                m.Content = message ?? "";
                m.Embeds = Array.Empty<Embed>();
                m.Components = new ComponentBuilder().Build();
            });
        }
        catch
        {
            try { await component.RespondAsync("Kunne ikke opdatere wizard. Prøv igen.", ephemeral: true); } catch { }
        }
    }

    private async Task UpdateDraftAsync(Action<TournamentCreateFlowState.Draft> mutate)
    {
        if (Context.Interaction is not SocketMessageComponent component)
            return;

        if (!_createFlow.TryGet(component.Message.Id, out var draft))
        {
            await component.RespondAsync("Flow udløbet. Kør `/tournament create` igen.", ephemeral: true);
            return;
        }

        try
        {
            mutate(draft);
            if (draft.Progression == TournamentProgression.SingleMatch)
                draft.AdvanceCount = 1;

            await component.UpdateAsync(m =>
            {
                m.Content = "";
                m.Embeds = new[] { BuildCreateFlowEmbed(draft) };
                m.Components = BuildCreateFlowComponents();
            });
        }
        catch
        {
            // Wrap'er respond i try/catch for at undgå, at en sekundær fejl efter en allerede mislykket UpdateAsync
            // efterlader interaktionen uden svar.
            try { await component.RespondAsync("Kunne ikke opdatere wizard. Prøv igen.", ephemeral: true); } catch { }
        }
    }

    private static Embed BuildCreateFlowEmbed(TournamentCreateFlowState.Draft d)
    {
        var progressionLabel = d.Progression == TournamentProgression.Bracket ? "Bracket" : "Single Match";
        var typeLabel = d.IsTeamBased ? "Team" : "Solo";
        var randomLabel = d.RandomizeSeeds ? "Ja" : "Nej";

        return new EmbedBuilder()
            .WithTitle($"Opret turnering: {d.Name}")
            .WithColor(new Color(0x58, 0x65, 0xF2))
            .WithDescription("Konfigurer turneringen og tryk **Bekræft**.")
            .AddField("Progression", progressionLabel, true)
            .AddField("Type", typeLabel, true)
            .AddField("Random seeding", randomLabel, true)
            .AddField("Match størrelse", d.PlayersPerMatch, true)
            .AddField("Går videre", d.AdvanceCount, true)
            .Build();
    }

    private static MessageComponent BuildCreateFlowComponents()
    {
        var ppm = new SelectMenuBuilder()
            .WithCustomId("tcf_ppm")
            .WithPlaceholder("Vælg match størrelse")
            .WithMinValues(1).WithMaxValues(1)
            .AddOption("2", "2")
            .AddOption("3", "3")
            .AddOption("4", "4")
            .AddOption("6", "6")
            .AddOption("8", "8")
            .AddOption("16", "16");

        var adv = new SelectMenuBuilder()
            .WithCustomId("tcf_adv")
            .WithPlaceholder("Vælg hvor mange går videre")
            .WithMinValues(1).WithMaxValues(1)
            .AddOption("1", "1")
            .AddOption("2", "2")
            .AddOption("3", "3")
            .AddOption("4", "4");

        return new ComponentBuilder()
            .WithButton("Bracket", "tcf_prog_bracket", ButtonStyle.Primary, row: 0)
            .WithButton("Single Match", "tcf_prog_single", ButtonStyle.Secondary, row: 0)
            .WithButton("Solo", "tcf_type_solo", ButtonStyle.Primary, row: 0)
            .WithButton("Team", "tcf_type_team", ButtonStyle.Secondary, row: 0)
            .WithButton("Random ON", "tcf_rand_on", ButtonStyle.Success, row: 1)
            .WithButton("Random OFF", "tcf_rand_off", ButtonStyle.Danger, row: 1)
            .WithButton("Bekræft", "tcf_confirm", ButtonStyle.Success, row: 1)
            .WithButton("Annullér", "tcf_cancel", ButtonStyle.Danger, row: 1)
            .WithSelectMenu(ppm, row: 2)
            .WithSelectMenu(adv, row: 3)
            .Build();
    }

    [SlashCommand("open", "Åbn tilmelding (admin)")]
    public async Task OpenAsync(
        [Summary(description: "Navn på turneringen")]
        [Autocomplete(typeof(TournamentAnyStatusAutocompleteHandler))]
        string navn)
    {
        await DeferAsync(ephemeral: true);
        if (Context.User is not SocketGuildUser gu || !_admin.IsAdmin(gu))
        {
            await FollowupAsync("Du har ikke rettigheder til den kommando.", ephemeral: true);
            return;
        }
        var (ok, message) = await _tournaments.SetStatusAsync(navn, TournamentStatus.Open);
        if (ok) await _audit.TryLogAsync($"[tournament.open] {Context.User} åbnede **{navn}**");
        await FollowupAsync(message, ephemeral: true);
    }

    [SlashCommand("close", "Luk tilmelding (admin)")]
    public async Task CloseAsync(
        [Summary(description: "Navn på turneringen")]
        [Autocomplete(typeof(TournamentAnyStatusAutocompleteHandler))]
        string navn)
    {
        await DeferAsync(ephemeral: true);
        if (Context.User is not SocketGuildUser gu || !_admin.IsAdmin(gu))
        {
            await FollowupAsync("Du har ikke rettigheder til den kommando.", ephemeral: true);
            return;
        }
        var (ok, message) = await _tournaments.SetStatusAsync(navn, TournamentStatus.Closed);
        if (ok) await _audit.TryLogAsync($"[tournament.close] {Context.User} lukkede **{navn}**");
        await FollowupAsync(message, ephemeral: true);
    }

    [SlashCommand("enroll", "Tilmeld dig en turnering")]
    public async Task EnrollAsync(
        [Summary(description: "Navn på turneringen")]
        [Autocomplete(typeof(TournamentAutocompleteHandler))]
        string navn)
    {
        await DeferAsync(ephemeral: true);
        var (ok, message) = await _tournaments.EnrollMeAsync(navn, Context.User.Id);
        await FollowupAsync(message, ephemeral: true);
    }

    [SlashCommand("leave", "Afmeld dig en turnering")]
    public async Task LeaveAsync(
        [Summary(description: "Navn på turneringen")]
        [Autocomplete(typeof(TournamentAutocompleteHandler))]
        string navn)
    {
        await DeferAsync(ephemeral: true);
        var (ok, message) = await _tournaments.LeaveMeAsync(navn, Context.User.Id);
        await FollowupAsync(message, ephemeral: true);
    }

    [SlashCommand("status", "Vis status for en turnering")]
    public async Task StatusAsync(
        [Summary(description: "Navn på turneringen")]
        [Autocomplete(typeof(TournamentAutocompleteHandler))]
        string navn)
    {
        await DeferAsync(ephemeral: true);
        var (ok, message) = await _tournaments.GetStatusLineAsync(navn);
        var enrollment = await _tournaments.GetEnrollmentOverviewAsync(navn);
        var (tournament, roundNumber, matches) = await _tournaments.GetLatestRoundSnapshotAsync(navn);

        var color = tournament?.Status switch
        {
            TournamentStatus.Draft => new Color(0x95, 0xA5, 0xA6),
            TournamentStatus.Open => new Color(0x2E, 0xCC, 0x71),
            TournamentStatus.Closed => new Color(0xE6, 0x7E, 0x22),
            TournamentStatus.Seeded => new Color(0x34, 0x98, 0xDB),
            TournamentStatus.Running => new Color(0x9B, 0x59, 0xB6),
            TournamentStatus.Done => new Color(0xF1, 0xC4, 0x0F),
            _ => new Color(0x34, 0x98, 0xDB)
        };

        var embed = new EmbedBuilder()
            .WithTitle(tournament is null ? navn : tournament.Name)
            .WithColor(color)
            .WithDescription(message)
            .Build();

        var embed2 = (Embed?)null;
        if (roundNumber is not null)
        {
            var b = new EmbedBuilder()
                .WithTitle($"Round {roundNumber}")
                .WithColor(color);

            if (matches.Count == 0)
            {
                b.WithDescription("Ingen matches i denne round.");
            }
            else
            {
                foreach (var m in matches)
                {
                    var statusLabel = m.status == MatchStatus.Completed ? "✅ Completed" : "🕒 Pending";
                    var players = m.players.Count > 0 ? string.Join(" • ", m.players) : "(ingen spillere)";
                    b.AddField($"Match {m.seed}", $"{statusLabel}\n{players}", inline: false);
                }
            }

            embed2 = b.Build();
        }

        if (embed2 is null)
        {
            await FollowupAsync(enrollment is null ? "" : enrollment, embeds: new[] { embed }, ephemeral: true);
        }
        else
        {
            var content = string.IsNullOrWhiteSpace(enrollment) ? "" : enrollment;
            await FollowupAsync(content, embeds: new[] { embed, embed2 }, ephemeral: true);
        }
    }

    [SlashCommand("seed", "Generér brackets (admin)")]
    public async Task SeedAsync(
        [Summary(description: "Navn på turneringen")]
        [Autocomplete(typeof(TournamentAnyStatusAutocompleteHandler))]
        string navn)
    {
        await DeferAsync(ephemeral: true);
        if (Context.User is not SocketGuildUser gu || !_admin.IsAdmin(gu))
        {
            await FollowupAsync("Du har ikke rettigheder til den kommando.", ephemeral: true);
            return;
        }
        var (ok, message) = await _brackets.SeedAsync(navn);
        if (ok) await _audit.TryLogAsync($"[tournament.seed] {Context.User} seedede **{navn}**");
        await FollowupAsync(message, ephemeral: true);
    }

    [SlashCommand("winner", "Vis vinderen (når turneringen er færdig)")]
    public async Task WinnerAsync(
        [Summary(description: "Navn på turneringen")]
        [Autocomplete(typeof(TournamentAnyStatusAutocompleteHandler))]
        string navn)
    {
        await DeferAsync(ephemeral: true);
        var line = await _tournaments.GetWinnerLineAsync(navn);
        await FollowupAsync(line ?? "Jeg kan ikke finde en turnering med det navn.", ephemeral: true);
    }
}

public enum TournamentType
{
    Solo = 0,
    Team = 1,
}

