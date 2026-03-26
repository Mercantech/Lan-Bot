using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentAnnouncements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tournament_announcements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_announcements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tournament_announcements_tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tournament_announcements_ChannelId_MessageId",
                table: "tournament_announcements",
                columns: new[] { "ChannelId", "MessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tournament_announcements_TournamentId",
                table: "tournament_announcements",
                column: "TournamentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tournament_announcements");
        }
    }
}
