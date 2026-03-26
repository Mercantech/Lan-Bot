using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lan_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lan_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LanEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RealName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RealNameNormalized = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_participants_lan_events_LanEventId",
                        column: x => x.LanEventId,
                        principalTable: "lan_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tournaments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LanEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsTeamBased = table.Column<bool>(type: "boolean", nullable: false),
                    RandomizeSeeds = table.Column<bool>(type: "boolean", nullable: false),
                    PlayersPerMatch = table.Column<int>(type: "integer", nullable: false),
                    AdvanceCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournaments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tournaments_lan_events_LanEventId",
                        column: x => x.LanEventId,
                        principalTable: "lan_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rounds_tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_teams_tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    Seed = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_matches_rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_members",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_members", x => new { x.TeamId, x.ParticipantId });
                    table.ForeignKey(
                        name: "FK_team_members_participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_team_members_teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tournament_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tournament_entries_participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tournament_entries_teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tournament_entries_tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "match_slots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    Placement = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_match_slots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_match_slots_matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_match_slots_participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_match_slots_teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_match_slots_MatchId_ParticipantId",
                table: "match_slots",
                columns: new[] { "MatchId", "ParticipantId" });

            migrationBuilder.CreateIndex(
                name: "IX_match_slots_MatchId_TeamId",
                table: "match_slots",
                columns: new[] { "MatchId", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_match_slots_ParticipantId",
                table: "match_slots",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_match_slots_TeamId",
                table: "match_slots",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_matches_RoundId_Seed",
                table: "matches",
                columns: new[] { "RoundId", "Seed" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_participants_LanEventId_DiscordUserId",
                table: "participants",
                columns: new[] { "LanEventId", "DiscordUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_participants_LanEventId_RealNameNormalized",
                table: "participants",
                columns: new[] { "LanEventId", "RealNameNormalized" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rounds_TournamentId_Number",
                table: "rounds",
                columns: new[] { "TournamentId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_team_members_ParticipantId",
                table: "team_members",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_teams_TournamentId_Name",
                table: "teams",
                columns: new[] { "TournamentId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tournament_entries_ParticipantId",
                table: "tournament_entries",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_entries_TeamId",
                table: "tournament_entries",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_entries_TournamentId_ParticipantId",
                table: "tournament_entries",
                columns: new[] { "TournamentId", "ParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tournaments_LanEventId_Name",
                table: "tournaments",
                columns: new[] { "LanEventId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "match_slots");

            migrationBuilder.DropTable(
                name: "team_members");

            migrationBuilder.DropTable(
                name: "tournament_entries");

            migrationBuilder.DropTable(
                name: "matches");

            migrationBuilder.DropTable(
                name: "participants");

            migrationBuilder.DropTable(
                name: "teams");

            migrationBuilder.DropTable(
                name: "rounds");

            migrationBuilder.DropTable(
                name: "tournaments");

            migrationBuilder.DropTable(
                name: "lan_events");
        }
    }
}
