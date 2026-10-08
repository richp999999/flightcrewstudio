using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightCrewStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAircraftTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AircraftTypeId",
                table: "Aircraft",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AircraftTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Manufacturer = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AircraftTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AircraftTypes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Aircraft_AircraftTypeId",
                table: "Aircraft",
                column: "AircraftTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AircraftTypes_UserId_Name",
                table: "AircraftTypes",
                columns: new[] { "UserId", "Name" },
                unique: true);

            // ---- Move existing type/manufacturer text into AircraftTypes and link by id -------
            // One type per user + name; the manufacturer comes from any aircraft of that type.
            migrationBuilder.Sql(@"
                INSERT INTO `AircraftTypes` (`UserId`, `Name`, `Manufacturer`, `CreatedAt`)
                SELECT `UserId`, TRIM(`Type`), MAX(NULLIF(UPPER(TRIM(`Manufacturer`)), '')), UTC_TIMESTAMP(6)
                FROM `Aircraft`
                WHERE `Type` IS NOT NULL AND TRIM(`Type`) <> ''
                GROUP BY `UserId`, TRIM(`Type`);");
            migrationBuilder.Sql(@"
                UPDATE `Aircraft` a JOIN `AircraftTypes` t ON t.`UserId` = a.`UserId` AND t.`Name` = TRIM(a.`Type`)
                SET a.`AircraftTypeId` = t.`Id`;");

            // The old text columns are no longer needed.
            migrationBuilder.DropColumn(
                name: "Manufacturer",
                table: "Aircraft");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Aircraft");

            migrationBuilder.AddForeignKey(
                name: "FK_Aircraft_AircraftTypes_AircraftTypeId",
                table: "Aircraft",
                column: "AircraftTypeId",
                principalTable: "AircraftTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Aircraft_AircraftTypes_AircraftTypeId",
                table: "Aircraft");

            migrationBuilder.DropTable(
                name: "AircraftTypes");

            migrationBuilder.DropIndex(
                name: "IX_Aircraft_AircraftTypeId",
                table: "Aircraft");

            migrationBuilder.DropColumn(
                name: "AircraftTypeId",
                table: "Aircraft");

            migrationBuilder.AddColumn<string>(
                name: "Manufacturer",
                table: "Aircraft",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Aircraft",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
