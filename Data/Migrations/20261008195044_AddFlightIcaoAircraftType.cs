using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightCrewStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFlightIcaoAircraftType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
    }
}
