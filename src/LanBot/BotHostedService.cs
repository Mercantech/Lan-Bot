using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using LanBot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LanBot.Discord;

namespace LanBot;

public sealed class BotHostedService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactions;
    private readonly IServiceProvider _services;
    private readonly ILogger<BotHostedService> _logger;
    private readonly BotOptions _options;
    private readonly TournamentAnnouncementService _announcements;

    public BotHostedService(
        DiscordSocketClient client,
        InteractionService interactions,
        IServiceProvider services,
        ILogger<BotHostedService> logger,
        IOptions<BotOptions> options,
        TournamentAnnouncementService announcements)
    {
        _client = client;
        _interactions = interactions;
        _services = services;
        _logger = logger;
        _options = options.Value;
        _announcements = announcements;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client.Log += msg =>
        {
            _logger.Log(MapLogLevel(msg.Severity), msg.Exception, "{Source}: {Message}", msg.Source, msg.Message);
            return Task.CompletedTask;
        };

        _interactions.Log += msg =>
        {
            _logger.Log(MapLogLevel(msg.Severity), msg.Exception, "{Source}: {Message}", msg.Source, msg.Message);
            return Task.CompletedTask;
        };

        _client.Ready += OnReadyAsync;
        _client.ReactionAdded += _announcements.HandleReactionAsync;
        _client.ReactionRemoved += _announcements.HandleReactionRemovedAsync;

        await EnsureDatabaseAsync(stoppingToken);

        await _client.LoginAsync(TokenType.Bot, _options.DiscordToken);
        await _client.StartAsync();

        _logger.LogInformation("Bot started. Waiting...");
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _client.StopAsync();
        await base.StopAsync(cancellationToken);
    }

    private async Task EnsureDatabaseAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LanBotDbContext>();
        await db.Database.MigrateAsync(ct);
    }

    private async Task OnReadyAsync()
    {
        using var scope = _services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<InteractionHandler>();
        await handler.InitializeAsync();

        _logger.LogInformation("Connected as {User}", _client.CurrentUser);
    }

    private static LogLevel MapLogLevel(LogSeverity severity) => severity switch
    {
        LogSeverity.Critical => LogLevel.Critical,
        LogSeverity.Error => LogLevel.Error,
        LogSeverity.Warning => LogLevel.Warning,
        LogSeverity.Info => LogLevel.Information,
        LogSeverity.Verbose => LogLevel.Debug,
        LogSeverity.Debug => LogLevel.Debug,
        _ => LogLevel.Information
    };
}

