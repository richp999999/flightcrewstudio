using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightCrewStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCrewRoleCountsAsFlightTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CountsAsFlightTime",
                table: "CrewRoles",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);   // existing roles (Instructor, FE, …) keep counting

            // Supernumerary seats aren't flight time; the role is created on demand, so set it by name.
            migrationBuilder.Sql("UPDATE CrewRoles SET CountsAsFlightTime = 0 WHERE Name = 'Supernumerary'");

            migrationBuilder.UpdateData(
                table: "CrewRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "CountsAsFlightTime",
                value: true);

            migrationBuilder.UpdateData(
                table: "CrewRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "CountsAsFlightTime",
                value: true);

            migrationBuilder.UpdateData(
                table: "CrewRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "CountsAsFlightTime",
                value: true);

            migrationBuilder.UpdateData(
                table: "CrewRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "CountsAsFlightTime",
                value: false);

            migrationBuilder.UpdateData(
                table: "CrewRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "CountsAsFlightTime",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CountsAsFlightTime",
                table: "CrewRoles");
        }
    }
}
