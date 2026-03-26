using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace LanBot.Discord;

public sealed class AdminService
{
    private readonly BotOptions _options;

    public AdminService(IOptions<BotOptions> options)
    {
        _options = options.Value;
    }

    public bool IsAdmin(SocketGuildUser user)
    {
        if (user.GuildPermissions.Administrator || user.GuildPermissions.ManageGuild)
            return true;

        if (_options.AdminRoleId is ulong roleId && user.Roles.Any(r => r.Id == roleId))
            return true;

        return false;
    }
}

