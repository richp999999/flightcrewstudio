using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightCrewStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExportFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LicenceNumber",
                table: "AspNetUsers",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LogbookName",
                table: "AspNetUsers",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IcaoCode",
                table: "AircraftTypes",
                type: "varchar(4)",
                maxLength: 4,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LicenceNumber",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LogbookName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IcaoCode",
                table: "AircraftTypes");
        }
    }
}
