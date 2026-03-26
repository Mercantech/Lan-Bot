using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LanBot.Discord;

public sealed class AuditLogService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<AuditLogService> _logger;
    private readonly BotOptions _options;

    public AuditLogService(DiscordSocketClient client, ILogger<AuditLogService> logger, IOptions<BotOptions> options)
    {
        _client = client;
        _logger = logger;
        _options = options.Value;
    }

    public async Task TryLogAsync(string message)
    {
        if (_options.AdminLogChannelId is not ulong channelId)
            return;

        try
        {
            var channel = _client.GetChannel(channelId) as IMessageChannel;
            if (channel is null)
                return;

            await channel.SendMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send audit log message");
        }
    }
}

