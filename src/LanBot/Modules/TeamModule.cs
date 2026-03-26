using Discord;
using Discord.Interactions;
using LanBot.Discord;
using Discord.WebSocket;

namespace LanBot;

[Group("team", "Hold-kommandoer")]
public sealed class TeamModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly TeamService _teams;
    private readonly AuditLogService _audit;
    private readonly AdminService _admin;

    public TeamModule(TeamService teams, AuditLogService audit, AdminService admin)
    {
        _teams = teams;
        _audit = audit;
        _admin = admin;
    }

    [SlashCommand("create", "Opret et hold i en hold-turnering (admin)")]
    public async Task CreateAsync(
        [Summary(description: "Turneringens navn")]
        [Autocomplete(typeof(TournamentAnyStatusAutocompleteHandler))]
        string turnering,
        [Summary(description: "Holdets navn")] string hold)
    {
        await DeferAsync(ephemeral: true);
        if (Context.User is not SocketGuildUser gu || !_admin.IsAdmin(gu))
        {
            await FollowupAsync("Du har ikke rettigheder til den kommando.", ephemeral: true);
            return;
        }
        var (ok, message) = await _teams.CreateTeamAsync(turnering, hold);
        if (ok) await _audit.TryLogAsync($"[team.create] {Context.User} oprettede hold **{hold}** i **{turnering}**");
        await FollowupAsync(message, ephemeral: true);
    }

    [SlashCommand("add", "Tilføj en bruger til et hold (admin)")]
    public async Task AddAsync(
        [Summary(description: "Turneringens navn")]
        [Autocomplete(typeof(TournamentAnyStatusAutocompleteHandler))]
        string turnering,
        [Summary(description: "Holdets navn")]
        [Autocomplete(typeof(TeamAutocompleteHandler))]
        string hold,
        [Summary(description: "Brugeren der skal på holdet")] IUser bruger)
    {
        await DeferAsync(ephemeral: true);
        if (Context.User is not SocketGuildUser gu || !_admin.IsAdmin(gu))
        {
            await FollowupAsync("Du har ikke rettigheder til den kommando.", ephemeral: true);
            return;
        }
        var (ok, message) = await _teams.AddMemberAsync(turnering, hold, bruger.Id);
        if (ok) await _audit.TryLogAsync($"[team.add] {Context.User} tilføjede {bruger} til **{hold}** i **{turnering}**");
        await FollowupAsync(message, ephemeral: true);
    }
}

