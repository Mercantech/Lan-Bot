using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentProgression : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Progression",
                table: "tournaments",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Progression",
                table: "tournaments");
        }
    }
}
