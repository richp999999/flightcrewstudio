using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightCrewStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMyCrewMember : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MyCrewMemberId",
                table: "GlobaLogCredentials",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GlobaLogCredentials_MyCrewMemberId",
                table: "GlobaLogCredentials",
                column: "MyCrewMemberId");

            migrationBuilder.AddForeignKey(
                name: "FK_GlobaLogCredentials_CrewMembers_MyCrewMemberId",
                table: "GlobaLogCredentials",
                column: "MyCrewMemberId",
                principalTable: "CrewMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GlobaLogCredentials_CrewMembers_MyCrewMemberId",
                table: "GlobaLogCredentials");

            migrationBuilder.DropIndex(
                name: "IX_GlobaLogCredentials_MyCrewMemberId",
                table: "GlobaLogCredentials");

            migrationBuilder.DropColumn(
                name: "MyCrewMemberId",
                table: "GlobaLogCredentials");
        }
    }
}
