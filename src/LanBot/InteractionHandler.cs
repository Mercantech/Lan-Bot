using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace LanBot;

public sealed class InteractionHandler
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactions;
    private readonly IServiceProvider _services;
    private readonly ILogger<InteractionHandler> _logger;
    private readonly BotOptions _options;

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
            await _interactions.ExecuteCommandAsync(ctx, _services);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle interaction");
            try
            {
                // Discord viser "This interaction failed" hvis der ikke bliver sendt en response tilbage.
                // Derfor forsøger vi at svare uanset om det er slash-command eller komponent (buttons/selects).
                if (interaction is SocketMessageComponent component)
                    await component.RespondAsync("Der skete en fejl. Prøv igen.", ephemeral: true);
                else
                    await interaction.RespondAsync("Der skete en fejl. Prøv igen.", ephemeral: true);
            }
            catch
            {
                // ignore follow-up errors
            }
        }
    }
}

