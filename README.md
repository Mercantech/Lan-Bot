# Lan-Bot

Discord bot til LAN-party (turneringer, tilmelding, brackets).

## Discord Developer Portal (opsæt bot)

- Opret en applikation i Discord Developer Portal.
- Gå til **Bot** → **Add Bot**.
- Kopiér token fra **Bot** → **Token** og gem det som `DISCORD_TOKEN` (secret).
- Invitér botten til jeres server via **OAuth2 → URL Generator**:
  - **Scopes**: `bot` + `applications.commands`
  - **Bot permissions**: giv kun det nødvendige (typisk: send beskeder + embeds)
- Under udvikling anbefales guild-sync af slash commands:
  - Sæt `GUILD_ID` til serverens id for hurtig udrulning af commands.

## Kør i Docker (Compose)

1. Kopiér `.env.example` til `.env` og udfyld `DISCORD_TOKEN` (og evt. `GUILD_ID`).
2. Start:

```bash
docker compose up -d --build
```

Botten kører sammen med Postgres og kører migrations ved startup.

## Miljøvariabler

- **`DISCORD_TOKEN`**: bot token fra Developer Portal
- **`POSTGRES_DB`**, **`POSTGRES_USER`**, **`POSTGRES_PASSWORD`**: Postgres credentials (Compose)
- **`POSTGRES_CONNECTION_STRING`**: sættes automatisk i `docker-compose.yml`
- **`GUILD_ID`**: (valgfri) guild til hurtig slash command sync
- **`ADMIN_ROLE_ID`**: (valgfri) rolle-id der giver admin-kommandoer adgang
- **`ADMIN_LOG_CHANNEL_ID`**: (valgfri) kanal-id til audit log
- **`TOURNAMENT_ANNOUNCEMENTS_CHANNEL_ID`**: (valgfri) kanal-id hvor nye turneringer annonceres (✅ reaction = tilmelding)
- **`LAN_EVENT_NAME`**: aktivt LAN-event navn (default: `Default LAN`)

## Kommandoer (MVP)

- **LAN**
  - `/lan join` – tilmeld LAN med rigtigt navn (unik pr. LAN)
  - `/lan me` – vis din registrering
- **Turnering**
  - `/tournament create` – opret turnering (admin)
  - `/tournament open` / `/tournament close` – åbn/luk tilmelding (admin)
  - `/tournament enroll` – tilmeld dig turnering
  - `/tournament seed` – generér Round 1 (admin)
  - `/tournament status` – status + match-oversigt (inkl. match-id)
  - `/tournament winner` – vis vinder (når afsluttet)
- **Hold** (til hold-turneringer)
  - `/team create` – opret hold (admin)
  - `/team add` – tilføj bruger til hold (admin)
- **Match**
  - `/match report` – rapportér placeringer (admin)
