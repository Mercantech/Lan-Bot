using LanBot.Web.Components;
using LanBot.Data;
using LanBot.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();

var conn =
    Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
    ?? builder.Configuration["POSTGRES_CONNECTION_STRING"]
    ?? "Host=localhost;Port=5432;Database=lanbot;Username=lanbot;Password=lanbot";

builder.Services.AddDbContext<LanBotDbContext>(o => o.UseNpgsql(conn));
builder.Services.AddScoped<TournamentReadService>();
builder.Services.AddScoped<TournamentAdminService>();
builder.Services.AddScoped<MatchAdminService>();

var discordClientId =
    Environment.GetEnvironmentVariable("DISCORD_CLIENT_ID")
    ?? builder.Configuration["DISCORD_CLIENT_ID"];
var discordClientSecret =
    Environment.GetEnvironmentVariable("DISCORD_CLIENT_SECRET")
    ?? builder.Configuration["DISCORD_CLIENT_SECRET"];
var discordAdminIdsRaw =
    Environment.GetEnvironmentVariable("DISCORD_ADMIN_IDS")
    ?? builder.Configuration["DISCORD_ADMIN_IDS"];
var discordAdminIds = (discordAdminIdsRaw ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .ToHashSet(StringComparer.Ordinal);

builder.Services.AddSingleton(new DiscordAdminOptions(discordAdminIds));

var authBuilder = builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = "Discord";
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
    });

var discordAuthEnabled = !string.IsNullOrWhiteSpace(discordClientId) && !string.IsNullOrWhiteSpace(discordClientSecret);
if (discordAuthEnabled)
{
    authBuilder.AddOAuth("Discord", options =>
    {
        options.ClientId = discordClientId!;
        options.ClientSecret = discordClientSecret!;
        options.CallbackPath = "/signin-discord";

        options.AuthorizationEndpoint = "https://discord.com/oauth2/authorize";
        options.TokenEndpoint = "https://discord.com/api/oauth2/token";
        options.UserInformationEndpoint = "https://discord.com/api/users/@me";

        options.Scope.Add("identify");
        options.SaveTokens = true;

        options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
        options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
        options.ClaimActions.MapJsonKey("urn:discord:avatar", "avatar");

        options.Events = new OAuthEvents
        {
            OnCreatingTicket = async context =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);

                using var response = await context.Backchannel.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    context.HttpContext.RequestAborted);

                response.EnsureSuccessStatusCode();
                using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
                context.RunClaimActions(payload.RootElement);

                var username = payload.RootElement.TryGetProperty("username", out var u) ? u.GetString() : null;
                var discriminator = payload.RootElement.TryGetProperty("discriminator", out var d) ? d.GetString() : null;
                if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(discriminator) && discriminator != "0")
                {
                    context.Identity?.AddClaim(new Claim("urn:discord:full_name", $"{username}#{discriminator}"));
                }
            }
        };
    });
}

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("AdminOnly", policy =>
        policy.RequireAuthenticatedUser().RequireAssertion(ctx =>
        {
            var id = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return !string.IsNullOrWhiteSpace(id) && discordAdminIds.Contains(id);
        }));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/login", async (HttpContext httpContext, string? returnUrl) =>
{
    if (!discordAuthEnabled)
        return Results.Problem("Discord OAuth er ikke konfigureret endnu. Saet DISCORD_CLIENT_ID og DISCORD_CLIENT_SECRET.", statusCode: 500);

    var redirectUri = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;
    await httpContext.ChallengeAsync("Discord", new AuthenticationProperties { RedirectUri = redirectUri });
    return Results.Empty;
});

app.MapGet("/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

app.Run();

public sealed record DiscordAdminOptions(HashSet<string> AllowedDiscordIds);
