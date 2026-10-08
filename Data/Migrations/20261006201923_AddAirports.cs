using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightCrewStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAirports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ArrivalAirportId",
                table: "Flights",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepartureAirportId",
                table: "Flights",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Airports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Icao = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Iata = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Country = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Airports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Airports_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Flights_ArrivalAirportId",
                table: "Flights",
                column: "ArrivalAirportId");

            migrationBuilder.CreateIndex(
                name: "IX_Flights_DepartureAirportId",
                table: "Flights",
                column: "DepartureAirportId");

            migrationBuilder.CreateIndex(
                name: "IX_Airports_UserId_Icao",
                table: "Airports",
                columns: new[] { "UserId", "Icao" },
                unique: true);

            // ---- Move existing departure/arrival text into Airports and link by id ----------
            // One airport per user + ICAO, taking IATA/name/country from any flight that has them.
            migrationBuilder.Sql(@"
                INSERT INTO `Airports` (`UserId`, `Icao`, `Iata`, `Name`, `Country`, `CreatedAt`)
                SELECT `UserId`, `Icao`, MAX(`Iata`), MAX(`Name`), MAX(`Country`), UTC_TIMESTAMP(6)
                FROM (
                    SELECT `UserId`, UPPER(TRIM(`DepIcao`)) AS `Icao`, NULLIF(UPPER(TRIM(`DepIata`)), '') AS `Iata`,
                           NULLIF(TRIM(`DepName`), '') AS `Name`, NULLIF(TRIM(`DepCountry`), '') AS `Country`
                    FROM `Flights` WHERE `DepIcao` IS NOT NULL AND TRIM(`DepIcao`) <> ''
                    UNION ALL
                    SELECT `UserId`, UPPER(TRIM(`ArrIcao`)), NULLIF(UPPER(TRIM(`ArrIata`)), ''),
                           NULLIF(TRIM(`ArrName`), ''), NULLIF(TRIM(`ArrCountry`), '')
                    FROM `Flights` WHERE `ArrIcao` IS NOT NULL AND TRIM(`ArrIcao`) <> ''
                ) x
                WHERE CHAR_LENGTH(`Icao`) <= 4
                GROUP BY `UserId`, `Icao`;");
            migrationBuilder.Sql(@"
                UPDATE `Flights` f JOIN `Airports` a ON a.`UserId` = f.`UserId` AND a.`Icao` = UPPER(TRIM(f.`DepIcao`))
                SET f.`DepartureAirportId` = a.`Id`;");
            migrationBuilder.Sql(@"
                UPDATE `Flights` f JOIN `Airports` a ON a.`UserId` = f.`UserId` AND a.`Icao` = UPPER(TRIM(f.`ArrIcao`))
                SET f.`ArrivalAirportId` = a.`Id`;");

            // The old text columns are no longer needed.
            migrationBuilder.DropColumn(
                name: "ArrCountry",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "ArrIata",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "ArrIcao",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "ArrName",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "DepCountry",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "DepIata",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "DepIcao",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "DepName",
                table: "Flights");

            migrationBuilder.AddForeignKey(
                name: "FK_Flights_Airports_ArrivalAirportId",
                table: "Flights",
                column: "ArrivalAirportId",
                principalTable: "Airports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Flights_Airports_DepartureAirportId",
                table: "Flights",
                column: "DepartureAirportId",
                principalTable: "Airports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Flights_Airports_ArrivalAirportId",
                table: "Flights");

            migrationBuilder.DropForeignKey(
                name: "FK_Flights_Airports_DepartureAirportId",
                table: "Flights");

            migrationBuilder.DropTable(
                name: "Airports");

            migrationBuilder.DropIndex(
                name: "IX_Flights_ArrivalAirportId",
                table: "Flights");

            migrationBuilder.DropIndex(
                name: "IX_Flights_DepartureAirportId",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "ArrivalAirportId",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "DepartureAirportId",
                table: "Flights");

            migrationBuilder.AddColumn<string>(
                name: "ArrCountry",
                table: "Flights",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ArrIata",
                table: "Flights",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ArrIcao",
                table: "Flights",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ArrName",
                table: "Flights",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DepCountry",
                table: "Flights",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DepIata",
                table: "Flights",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DepIcao",
                table: "Flights",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DepName",
                table: "Flights",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
