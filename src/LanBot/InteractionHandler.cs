using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Threading;
using Discord.Net;

namespace LanBot;

public sealed class InteractionHandler
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactions;
    private readonly IServiceProvider _services;
    private readonly ILogger<InteractionHandler> _logger;
    private readonly BotOptions _options;
    private int _initialized;

    public InteractionHandler(
        DiscordSocketClient client,
        InteractionService interactions,
        IServiceProvider services,
        ILogger<InteractionHandler> logger,
        IOptions<BotOptions> options)
    {
        _client = client;
        _interactions = interactions;
        _services = services;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InitializeAsync()
    {
        // Discord "Ready" kan trigges flere gange (fx reconnect). Vi må kun registrere handlers/modules én gang.
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            _logger.LogDebug("Interaction handler already initialized; skipping re-registration.");
            return;
        }

        _client.InteractionCreated += HandleInteractionAsync;

        await _interactions.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);

        if (_options.GuildId is ulong guildId)
        {
            await _interactions.RegisterCommandsToGuildAsync(guildId, true);
            _logger.LogInformation("Registered slash commands to guild {GuildId}", guildId);
        }
        else
        {
            await _interactions.RegisterCommandsGloballyAsync(true);
            _logger.LogInformation("Registered slash commands globally");
        }
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        try
        {
            var ctx = new SocketInteractionContext(_client, interaction);
            var result = await _interactions.ExecuteCommandAsync(ctx, _services);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Interaction execution failed: {Error} - {Reason}", result.Error, result.ErrorReason);
            }

            // Autocomplete kan ikke besvares med almindelig RespondAsync. Derfor svarer vi kun
            // fallback på interaktionstyper, der understøtter standard response.
            if (!interaction.HasResponded && SupportsStandardResponse(interaction))
            {
                await interaction.RespondAsync("Der skete en fejl. Prøv igen.", ephemeral: true);
            }
        }
        catch (Exception ex)
        {
            // Hvis samme interaction allerede er ack'et (fx dobbelt håndtering ved drift),
            // så undgår vi støj og ekstra fejl-svar.
            if (IsAlreadyAcknowledgedOrExpired(ex))
            {
                _logger.LogWarning(ex, "Interaction already acknowledged or expired; skipping duplicate response.");
                return;
            }

            _logger.LogError(ex, "Failed to handle interaction");
            try
            {
                if (!interaction.HasResponded && SupportsStandardResponse(interaction))
                    await interaction.RespondAsync("Der skete en fejl. Prøv igen.", ephemeral: true);
            }
            catch
            {
                // ignore follow-up errors
            }
        }
    }

    private static bool SupportsStandardResponse(SocketInteraction interaction) =>
        interaction is SocketSlashCommand
        || interaction is SocketMessageComponent
        || interaction is SocketModal;

    private static bool IsAlreadyAcknowledgedOrExpired(Exception ex)
    {
        if (ex is HttpException httpEx)
        {
            return httpEx.DiscordCode.HasValue
                && (int)httpEx.DiscordCode.Value is 40060 or 10062;
        }

        if (ex is InteractionException iex && iex.InnerException is HttpException innerHttpEx)
        {
            return innerHttpEx.DiscordCode.HasValue
                && (int)innerHttpEx.DiscordCode.Value is 40060 or 10062;
        }

        return false;
    }
}

