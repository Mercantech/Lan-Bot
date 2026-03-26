using Discord.Interactions;

namespace LanBot;

[Group("lan", "LAN-kommandoer")]
public sealed class LanModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ParticipantService _participants;

    public LanModule(ParticipantService participants)
    {
        _participants = participants;
    }

    [SlashCommand("join", "Tilmeld dig LAN med dit rigtige navn")]
    public async Task JoinAsync([Summary(description: "Dit rigtige navn (unik på LAN)")] string navn)
    {
        await DeferAsync(ephemeral: true);

        var (ok, message, _) = await _participants.RegisterOrUpdateAsync(Context.User.Id, navn);
        await FollowupAsync(message, ephemeral: true);
    }

    [SlashCommand("me", "Vis din LAN-tilmelding")]
    public async Task MeAsync()
    {
        await DeferAsync(ephemeral: true);

        var me = await _participants.GetMeAsync(Context.User.Id);
        if (me is null)
        {
            await FollowupAsync("Du er ikke tilmeldt endnu. Brug `/lan join`.", ephemeral: true);
            return;
        }

        await FollowupAsync($"Du er tilmeldt som **{me.RealName}**.", ephemeral: true);
    }
}

