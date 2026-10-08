using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightCrewStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRemarksAndEditedLocally : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EditedAt",
                table: "Flights",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EditedLocally",
                table: "Flights",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "Flights",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EditedAt",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "EditedLocally",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "Flights");
        }
    }
}
