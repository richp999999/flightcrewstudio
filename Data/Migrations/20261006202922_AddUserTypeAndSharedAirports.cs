using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightCrewStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserTypeAndSharedAirports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Airports_AspNetUsers_UserId",
                table: "Airports");

            migrationBuilder.DropIndex(
                name: "IX_Airports_UserId_Icao",
                table: "Airports");

            // ---- Airports become one shared list: merge duplicates (same ICAO) across users -----
            // Keep the lowest id per ICAO, fill its blanks from the duplicates, re-point flights, delete the rest.
            migrationBuilder.Sql(@"
                CREATE TEMPORARY TABLE `AirportKeep` AS
                SELECT `Icao`, MIN(`Id`) AS `KeepId`, MAX(`Iata`) AS `Iata`, MAX(`Name`) AS `Name`, MAX(`Country`) AS `Country`
                FROM `Airports` GROUP BY `Icao`;");
            migrationBuilder.Sql(@"
                UPDATE `Airports` a JOIN `AirportKeep` k ON k.`KeepId` = a.`Id`
                SET a.`Iata` = COALESCE(a.`Iata`, k.`Iata`), a.`Name` = COALESCE(a.`Name`, k.`Name`), a.`Country` = COALESCE(a.`Country`, k.`Country`);");
            migrationBuilder.Sql(@"
                UPDATE `Flights` f JOIN `Airports` a ON a.`Id` = f.`DepartureAirportId` JOIN `AirportKeep` k ON k.`Icao` = a.`Icao`
                SET f.`DepartureAirportId` = k.`KeepId` WHERE f.`DepartureAirportId` <> k.`KeepId`;");
            migrationBuilder.Sql(@"
                UPDATE `Flights` f JOIN `Airports` a ON a.`Id` = f.`ArrivalAirportId` JOIN `AirportKeep` k ON k.`Icao` = a.`Icao`
                SET f.`ArrivalAirportId` = k.`KeepId` WHERE f.`ArrivalAirportId` <> k.`KeepId`;");
            migrationBuilder.Sql(@"
                DELETE a FROM `Airports` a JOIN `AirportKeep` k ON k.`Icao` = a.`Icao` WHERE a.`Id` <> k.`KeepId`;");
            migrationBuilder.Sql("DROP TEMPORARY TABLE `AirportKeep`;");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Airports");

            migrationBuilder.AddColumn<int>(
                name: "UserType",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Airports_Icao",
                table: "Airports",
                column: "Icao",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Airports_Icao",
                table: "Airports");

            migrationBuilder.DropColumn(
                name: "UserType",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Airports",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Airports_UserId_Icao",
                table: "Airports",
                columns: new[] { "UserId", "Icao" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Airports_AspNetUsers_UserId",
                table: "Airports",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
