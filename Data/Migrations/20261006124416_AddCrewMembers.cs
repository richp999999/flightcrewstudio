using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightCrewStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCrewMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Old crew rows (staff number + role only) can't be mapped to crew members; they are
            // re-created from GlobaLog's flight pages on the next sync.
            migrationBuilder.Sql("DELETE FROM `FlightCrew`;");


            migrationBuilder.DropColumn(
                name: "LoadId",
                table: "FlightCrew");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "FlightCrew",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "CrewMemberId",
                table: "FlightCrew",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Position",
                table: "FlightCrew",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CrewMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StaffNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LatestRole = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrewMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrewMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_FlightCrew_CrewMemberId",
                table: "FlightCrew",
                column: "CrewMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_FlightCrew_FlightId_CrewMemberId",
                table: "FlightCrew",
                columns: new[] { "FlightId", "CrewMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_CrewMembers_UserId_NormalizedName",
                table: "CrewMembers",
                columns: new[] { "UserId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrewMembers_UserId_StaffNumber",
                table: "CrewMembers",
                columns: new[] { "UserId", "StaffNumber" },
                unique: true);

            // Drop the old single-column index only now: MySQL needs an index on FlightId for the
            // FK_FlightCrew_Flights_FlightId foreign key, and IX_FlightCrew_FlightId_CrewMemberId
            // (created above, FlightId first) takes over that job.
            migrationBuilder.DropIndex(
                name: "IX_FlightCrew_FlightId",
                table: "FlightCrew");

            migrationBuilder.AddForeignKey(
                name: "FK_FlightCrew_CrewMembers_CrewMemberId",
                table: "FlightCrew",
                column: "CrewMemberId",
                principalTable: "CrewMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FlightCrew_CrewMembers_CrewMemberId",
                table: "FlightCrew");

            migrationBuilder.DropTable(
                name: "CrewMembers");

            migrationBuilder.DropIndex(
                name: "IX_FlightCrew_CrewMemberId",
                table: "FlightCrew");

            migrationBuilder.CreateIndex(
                name: "IX_FlightCrew_FlightId",
                table: "FlightCrew",
                column: "FlightId");

            migrationBuilder.DropIndex(
                name: "IX_FlightCrew_FlightId_CrewMemberId",
                table: "FlightCrew");

            migrationBuilder.DropColumn(
                name: "CrewMemberId",
                table: "FlightCrew");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "FlightCrew");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "FlightCrew",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LoadId",
                table: "FlightCrew",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

        }
    }
}
