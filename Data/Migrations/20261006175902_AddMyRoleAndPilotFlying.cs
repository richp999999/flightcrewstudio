using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightCrewStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMyRoleAndPilotFlying : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MyRole",
                table: "Flights",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "PilotFlying",
                table: "Flights",
                type: "tinyint(1)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MyRole",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "PilotFlying",
                table: "Flights");
        }
    }
}
