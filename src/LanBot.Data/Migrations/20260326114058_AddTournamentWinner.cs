using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentWinner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WinnerParticipantId",
                table: "tournaments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WinnerTeamId",
                table: "tournaments",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WinnerParticipantId",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "WinnerTeamId",
                table: "tournaments");
        }
    }
}
