using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightCrewStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveIcaoAircraftTypeToAircraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. The ICAO type now belongs to the aircraft.
            migrationBuilder.AddColumn<int>(
                name: "IcaoAircraftTypeId",
                table: "Aircraft",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Aircraft_IcaoAircraftTypeId",
                table: "Aircraft",
                column: "IcaoAircraftTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Aircraft_IcaoAircraftTypes_IcaoAircraftTypeId",
                table: "Aircraft",
                column: "IcaoAircraftTypeId",
                principalTable: "IcaoAircraftTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 2. Keep what was already linked: each aircraft takes the type of its latest flight that had one.
            migrationBuilder.Sql(@"
                UPDATE Aircraft a
                SET a.IcaoAircraftTypeId = (
                    SELECT f.IcaoAircraftTypeId FROM Flights f
                    WHERE f.AircraftId = a.Id AND f.IcaoAircraftTypeId IS NOT NULL
                    ORDER BY f.OffBlock DESC LIMIT 1)
                WHERE a.IcaoAircraftTypeId IS NULL;");

            // 3. Then remove it from flights.
            migrationBuilder.DropForeignKey(
                name: "FK_Flights_IcaoAircraftTypes_IcaoAircraftTypeId",
                table: "Flights");

            migrationBuilder.DropIndex(
                name: "IX_Flights_IcaoAircraftTypeId",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "IcaoAircraftTypeId",
                table: "Flights");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Aircraft_IcaoAircraftTypes_IcaoAircraftTypeId",
                table: "Aircraft");

            migrationBuilder.DropIndex(
                name: "IX_Aircraft_IcaoAircraftTypeId",
                table: "Aircraft");

            migrationBuilder.DropColumn(
                name: "IcaoAircraftTypeId",
                table: "Aircraft");

            migrationBuilder.AddColumn<int>(
                name: "IcaoAircraftTypeId",
                table: "Flights",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Flights_IcaoAircraftTypeId",
                table: "Flights",
                column: "IcaoAircraftTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Flights_IcaoAircraftTypes_IcaoAircraftTypeId",
                table: "Flights",
                column: "IcaoAircraftTypeId",
                principalTable: "IcaoAircraftTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
