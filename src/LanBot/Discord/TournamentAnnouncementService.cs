using Discord;
using Discord.WebSocket;
using LanBot.Data;
using LanBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LanBot.Discord;

public sealed class TournamentAnnouncementService
{
    private static readonly Emoji JoinEmoji = new("✅");

    private readonly DiscordSocketClient _client;
    private readonly LanBotDbContext _db;
    private readonly ILogger<TournamentAnnouncementService> _logger;
    private readonly BotOptions _options;
    private readonly TournamentService _tournaments;
    private readonly ParticipantService _participants;

    public TournamentAnnouncementService(
        DiscordSocketClient client,
        LanBotDbContext db,
        ILogger<TournamentAnnouncementService> logger,
        IOptions<BotOptions> options,
        TournamentService tournaments,
        ParticipantService participants)
    {
        _client = client;
        _db = db;
        _logger = logger;
        _options = options.Value;
        _tournaments = tournaments;
        _participants = participants;
    }

    public async Task TryAnnounceTournamentAsync(Tournament tournament, SocketUser createdBy, ulong? channelIdOverride = null)
    {
        var channelId = channelIdOverride ?? _options.TournamentAnnouncementsChannelId;
        if (channelId is not ulong resolvedChannelId)
            return;

        var channel = _client.GetChannel(resolvedChannelId) as IMessageChannel;
        if (channel is null)
            return;

        var existing = await _db.TournamentAnnouncements.AnyAsync(x => x.TournamentId == tournament.Id);
        if (existing)
            return;

        var embed = new EmbedBuilder()
            .WithTitle($"Ny turnering: {tournament.Name}")
            .WithDescription("Reagér med ✅ for at tilmelde dig.")
            .AddField("Status", tournament.Status.ToString(), true)
            .AddField("Progression", tournament.Progression == TournamentProgression.Bracket ? "Bracket" : "Single Match", true)
            .AddField("Type", tournament.IsTeamBased ? "Hold" : "Solo", true)
            .AddField("Format", $"{tournament.PlayersPerMatch} pr. match → top {tournament.AdvanceCount} går videre", false)
            .WithFooter($"Oprettet af {createdBy}")
            .WithCurrentTimestamp()
            .Build();

        var msg = await channel.SendMessageAsync(embed: embed);
        await msg.AddReactionAsync(JoinEmoji);

        _db.TournamentAnnouncements.Add(new TournamentAnnouncement
        {
            TournamentId = tournament.Id,
            ChannelId = resolvedChannelId,
            MessageId = msg.Id,
        });
        await _db.SaveChangesAsync();
    }

    public async Task HandleReactionAsync(Cacheable<IUserMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> cachedChannel, SocketReaction reaction)
    {
        try
        {
            if (reaction.UserId == _client.CurrentUser.Id)
                return;

            if (reaction.Emote.Name != JoinEmoji.Name)
                return;

            var channelId = cachedChannel.Id;
            var messageId = cachedMessage.Id;

            var announcement = await _db.TournamentAnnouncements
                .Include(a => a.Tournament)
                .FirstOrDefaultAsync(a => a.ChannelId == channelId && a.MessageId == messageId);

            if (announcement?.Tournament is null)
                return;

            var tournament = announcement.Tournament;
            if (tournament.Status is TournamentStatus.Running or TournamentStatus.Done)
                return;

            // Kræv at man er registreret på LAN (rigtigt navn).
            var me = await _participants.GetMeAsync(reaction.UserId);
            if (me is null)
            {
                var u = reaction.User.IsSpecified ? reaction.User.Value : _client.GetUser(reaction.UserId);
                if (u is not null)
                {
                    var dmChannel = await u.CreateDMChannelAsync();
                    await dmChannel.SendMessageAsync($"Du skal først tilmelde dig LAN med `/lan join`, før du kan tilmelde dig **{tournament.Name}**.");
                }
                return;
            }

            var result = await _tournaments.EnrollMeAsync(tournament.Name, reaction.UserId);

            var user = reaction.User.IsSpecified ? reaction.User.Value : _client.GetUser(reaction.UserId);
            if (user is null)
                return;

            var dmChannel2 = await user.CreateDMChannelAsync();
            await dmChannel2.SendMessageAsync(result.ok
                ? $"Du er tilmeldt **{tournament.Name}**."
                : $"Kunne ikke tilmelde dig **{tournament.Name}**: {result.message}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to handle tournament reaction enrollment");
        }
    }

    public async Task HandleReactionRemovedAsync(Cacheable<IUserMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> cachedChannel, SocketReaction reaction)
    {
        try
        {
            if (reaction.UserId == _client.CurrentUser.Id)
                return;

            if (reaction.Emote.Name != JoinEmoji.Name)
                return;

            var channelId = cachedChannel.Id;
            var messageId = cachedMessage.Id;

            var announcement = await _db.TournamentAnnouncements
                .Include(a => a.Tournament)
                .FirstOrDefaultAsync(a => a.ChannelId == channelId && a.MessageId == messageId);

            if (announcement?.Tournament is null)
                return;

            var tournament = announcement.Tournament;
            if (tournament.Status is TournamentStatus.Running or TournamentStatus.Done)
                return;

            var result = await _tournaments.LeaveMeAsync(tournament.Name, reaction.UserId);

            var user = reaction.User.IsSpecified ? reaction.User.Value : _client.GetUser(reaction.UserId);
            if (user is null)
                return;

            var dm = await user.CreateDMChannelAsync();
            await dm.SendMessageAsync(result.ok
                ? $"Du er afmeldt **{tournament.Name}**."
                : $"Kunne ikke afmelde dig **{tournament.Name}**: {result.message}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to handle tournament reaction removal");
        }
    }
}

