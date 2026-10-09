using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightCrewStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUserAircraftTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Before the old per-user types go, give any aircraft without an ICAO type the one its
            // old type pointed to: first by the ICAO code entered on it, then by manufacturer + model
            // exactly as ICAO names them (how GlobaLog names them, e.g. AIRBUS / A-321neo).
            migrationBuilder.Sql(@"
                UPDATE Aircraft a
                JOIN AircraftTypes t ON t.Id = a.AircraftTypeId
                JOIN IcaoAircraftTypes i ON i.Designator = UPPER(t.IcaoCode)
                SET a.IcaoAircraftTypeId = i.Id
                WHERE a.IcaoAircraftTypeId IS NULL;");
            migrationBuilder.Sql(@"
                UPDATE Aircraft a
                JOIN AircraftTypes t ON t.Id = a.AircraftTypeId
                JOIN Manufacturers m ON m.Code = UPPER(t.Manufacturer)
                JOIN IcaoAircraftTypes i ON i.ManufacturerId = m.Id AND i.Model = t.Name
                SET a.IcaoAircraftTypeId = i.Id
                WHERE a.IcaoAircraftTypeId IS NULL;");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IcaoCode = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Manufacturer = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
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

            migrationBuilder.AddForeignKey(
                name: "FK_Aircraft_AircraftTypes_AircraftTypeId",
                table: "Aircraft",
                column: "AircraftTypeId",
                principalTable: "AircraftTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
