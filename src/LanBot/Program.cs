using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using LanBot;
using LanBot.Data;
using LanBot.Discord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(cfg =>
    {
        cfg.AddEnvironmentVariables();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddSimpleConsole(o =>
        {
            o.SingleLine = true;
            o.TimestampFormat = "HH:mm:ss ";
        });
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddOptions<BotOptions>()
            .Bind(ctx.Configuration)
            .Validate(o => !string.IsNullOrWhiteSpace(o.DiscordToken), "DISCORD_TOKEN is required")
            .Validate(o => !string.IsNullOrWhiteSpace(o.PostgresConnectionString), "POSTGRES_CONNECTION_STRING is required")
            .ValidateOnStart();

        var opts = ctx.Configuration.Get<BotOptions>() ?? new BotOptions();

        services.AddDbContext<LanBotDbContext>(db =>
            db.UseNpgsql(opts.PostgresConnectionString));

        services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.GuildMessageReactions,
            LogGatewayIntentWarnings = false,
            MessageCacheSize = 50,
        }));

        services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()));
        services.AddSingleton<InteractionHandler>();
        services.AddSingleton<AdminService>();
        services.AddSingleton<AuditLogService>();
        services.AddSingleton<TournamentAnnouncementService>();
        services.AddSingleton<MatchReportUiState>();
        services.AddSingleton<TournamentCreateFlowState>();
        services.AddSingleton<LanEventService>();
        services.AddSingleton<ParticipantService>();
        services.AddSingleton<TournamentService>();
        services.AddSingleton<BracketService>();
        services.AddSingleton<TeamService>();
        services.AddSingleton<MatchService>();

        services.AddHostedService<BotHostedService>();
    })
    .Build();

await host.RunAsync();

public sealed class BotOptions
{
    public string DiscordToken { get; init; } = Environment.GetEnvironmentVariable("DISCORD_TOKEN") ?? "";
    public string PostgresConnectionString { get; init; } = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING") ?? "";

    public ulong? GuildId { get; init; } = TryParseUlong(Environment.GetEnvironmentVariable("GUILD_ID"));
    public ulong? AdminRoleId { get; init; } = TryParseUlong(Environment.GetEnvironmentVariable("ADMIN_ROLE_ID"));
    public ulong? AdminLogChannelId { get; init; } = TryParseUlong(Environment.GetEnvironmentVariable("ADMIN_LOG_CHANNEL_ID"));
    public ulong? TournamentAnnouncementsChannelId { get; init; } =
        TryParseUlong(Environment.GetEnvironmentVariable("TOURNAMENT_ANNOUNCEMENTS_CHANNEL_ID"));

    public string LanEventName { get; init; } = Environment.GetEnvironmentVariable("LAN_EVENT_NAME") ?? "Default LAN";

    private static ulong? TryParseUlong(string? value) =>
        ulong.TryParse(value, out var parsed) ? parsed : null;
}
