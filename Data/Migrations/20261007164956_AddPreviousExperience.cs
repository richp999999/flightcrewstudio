using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightCrewStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPreviousExperience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PreviousExperience",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TotalMinutes = table.Column<int>(type: "int", nullable: false),
                    SingleEngineMinutes = table.Column<int>(type: "int", nullable: false),
                    MultiEngineMinutes = table.Column<int>(type: "int", nullable: false),
                    MultiPilotMinutes = table.Column<int>(type: "int", nullable: false),
                    NightMinutes = table.Column<int>(type: "int", nullable: false),
                    IfrMinutes = table.Column<int>(type: "int", nullable: false),
                    PicMinutes = table.Column<int>(type: "int", nullable: false),
                    CoPilotMinutes = table.Column<int>(type: "int", nullable: false),
                    DualMinutes = table.Column<int>(type: "int", nullable: false),
                    InstructorMinutes = table.Column<int>(type: "int", nullable: false),
                    SimulatorMinutes = table.Column<int>(type: "int", nullable: false),
                    TakeoffsDay = table.Column<int>(type: "int", nullable: false),
                    TakeoffsNight = table.Column<int>(type: "int", nullable: false),
                    LandingsDay = table.Column<int>(type: "int", nullable: false),
                    LandingsNight = table.Column<int>(type: "int", nullable: false),
                    InstrumentApproaches = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreviousExperience", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreviousExperience_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PreviousExperience_UserId",
                table: "PreviousExperience",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PreviousExperience");
        }
    }
}
