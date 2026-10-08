using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightCrewStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAircraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AircraftId",
                table: "Flights",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Aircraft",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Registration = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Manufacturer = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aircraft", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Aircraft_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Flights_AircraftId",
                table: "Flights",
                column: "AircraftId");

            migrationBuilder.CreateIndex(
                name: "IX_Aircraft_UserId_Registration",
                table: "Aircraft",
                columns: new[] { "UserId", "Registration" },
                unique: true);

            // ---- Move existing registrations/types into Aircraft and link by id ---------------
            // Registrations are normalised (no hyphen/spaces, upper-case); one row per user + registration.
            const string reg = "UPPER(REPLACE(REPLACE(TRIM(`AircraftRegistration`), '-', ''), ' ', ''))";
            migrationBuilder.Sql($@"
                INSERT INTO `Aircraft` (`UserId`, `Registration`, `Type`, `Manufacturer`, `CreatedAt`)
                SELECT `UserId`, {reg}, MAX(NULLIF(TRIM(`AircraftIcao`), '')), NULL, UTC_TIMESTAMP(6)
                FROM `Flights`
                WHERE `AircraftRegistration` IS NOT NULL AND TRIM(`AircraftRegistration`) <> ''
                GROUP BY `UserId`, {reg};");
            migrationBuilder.Sql($@"
                UPDATE `Flights` f JOIN `Aircraft` a
                  ON a.`UserId` = f.`UserId` AND a.`Registration` = {reg.Replace("`AircraftRegistration`", "f.`AircraftRegistration`")}
                SET f.`AircraftId` = a.`Id`;");

            // The old text columns are no longer needed.
            migrationBuilder.DropColumn(
                name: "AircraftIcao",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "AircraftRegistration",
                table: "Flights");

            migrationBuilder.AddForeignKey(
                name: "FK_Flights_Aircraft_AircraftId",
                table: "Flights",
                column: "AircraftId",
                principalTable: "Aircraft",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Flights_Aircraft_AircraftId",
                table: "Flights");

            migrationBuilder.DropTable(
                name: "Aircraft");

            migrationBuilder.DropIndex(
                name: "IX_Flights_AircraftId",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "AircraftId",
                table: "Flights");

            migrationBuilder.AddColumn<string>(
                name: "AircraftIcao",
                table: "Flights",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AircraftRegistration",
                table: "Flights",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
