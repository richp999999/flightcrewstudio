using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightCrewStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFlightSourceAndLoggedFigures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LandingsDay",
                table: "Flights",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LandingsNight",
                table: "Flights",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoggedCoPilotMinutes",
                table: "Flights",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoggedDualMinutes",
                table: "Flights",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoggedInstructorMinutes",
                table: "Flights",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoggedMultiEngineMinutes",
                table: "Flights",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoggedMultiPilotMinutes",
                table: "Flights",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoggedPicMinutes",
                table: "Flights",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoggedSingleEngineMinutes",
                table: "Flights",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Flights",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "TakeoffsDay",
                table: "Flights",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TakeoffsNight",
                table: "Flights",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LandingsDay",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "LandingsNight",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "LoggedCoPilotMinutes",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "LoggedDualMinutes",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "LoggedInstructorMinutes",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "LoggedMultiEngineMinutes",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "LoggedMultiPilotMinutes",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "LoggedPicMinutes",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "LoggedSingleEngineMinutes",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "TakeoffsDay",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "TakeoffsNight",
                table: "Flights");
        }
    }
}
