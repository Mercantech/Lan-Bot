using LanBot.Web.Components;
using LanBot.Data;
using LanBot.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
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
var publicBaseUrlRaw =
    Environment.GetEnvironmentVariable("PUBLIC_BASE_URL")
    ?? builder.Configuration["PUBLIC_BASE_URL"];
string? publicBaseUrl = null;
Uri? publicBaseUri = null;
if (!string.IsNullOrWhiteSpace(publicBaseUrlRaw))
{
    publicBaseUrl = publicBaseUrlRaw.Trim().TrimEnd('/');
    if (!publicBaseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        && !publicBaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
        publicBaseUrl = $"https://{publicBaseUrl}";
    }

    if (Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var parsed))
    {
        publicBaseUri = parsed;
    }
}
var discordAdminIds = (discordAdminIdsRaw ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .ToHashSet(StringComparer.Ordinal);
var discordAuthEnabled = !string.IsNullOrWhiteSpace(discordClientId) && !string.IsNullOrWhiteSpace(discordClientSecret);

builder.Services.AddSingleton(new DiscordAdminOptions(discordAdminIds));

var authBuilder = builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        // Brug altid cookie-challenge. Det sender uautoriserede brugere til `/login`,
        // som igen laver Discord OAuth-challenge, så correlation/“state”-cookies matcher callback-URL'en.
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
    });

if (discordAuthEnabled)
{
    authBuilder.AddOAuth("Discord", options =>
    {
        options.ClientId = discordClientId!;
        options.ClientSecret = discordClientSecret!;
        options.CallbackPath = "/signin-discord";
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

        // Under OAuth skal correlation-cookie kunne sendes tilbage fra Discord (cross-site redirect).
        // Vi beholder default path (callback-stien) for at undgå unødigt store Cookie-headers på andre routes.
        options.CorrelationCookie.Path = "/signin-discord";
        options.CorrelationCookie.SameSite = SameSiteMode.None;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;

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
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
    // Vigtigt bag reverse proxy: lad forwarding headers slå igennem, så `Request.Scheme`/`Host`
    // matcher den eksterne URL.
});
app.Use((context, next) =>
{
    // Når vi kører bag en reverse proxy (fx Cloudflare), kan `Request.Scheme`/`Host`
    // ellers ende med at være `http`/intern host. Det kan ødelægge OAuth/cookie-flowet.
    // Derfor normaliserer vi til `PUBLIC_BASE_URL`, hvis den er sat, ellers bruger vi
    // `X-Forwarded-Proto` hvis muligt.
    if (publicBaseUri is not null)
    {
        context.Request.Scheme = publicBaseUri.Scheme;
        context.Request.Host = publicBaseUri.IsDefaultPort
            ? new HostString(publicBaseUri.Host)
            : new HostString(publicBaseUri.Host, publicBaseUri.Port);
    }
    else
    {
        var forwardedProto = context.Request.Headers["X-Forwarded-Proto"].ToString();
        if (!string.IsNullOrWhiteSpace(forwardedProto))
        {
            // Antag `https` når proxy siger det. Det hjælper bl.a. med at få korrekt
            // callback/cookie behaviour.
            context.Request.Scheme = forwardedProto;
        }
        else
        {
            // Cloudflare sender typisk scheme i CF-Visitor, selv hvis X-Forwarded-Proto ikke er sat mod origin.
            var cfVisitor = context.Request.Headers["CF-Visitor"].ToString();
            if (!string.IsNullOrWhiteSpace(cfVisitor))
            {
                try
                {
                    using var doc = JsonDocument.Parse(cfVisitor);
                    if (doc.RootElement.TryGetProperty("scheme", out var s))
                    {
                        var sch = s.GetString();
                        if (!string.IsNullOrWhiteSpace(sch))
                            context.Request.Scheme = sch;
                    }
                }
                catch
                {
                    // ignore malformed header
                }
            }
        }
    }

    return next();
});

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
