using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FlightCrewStudio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCrewRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MyCrewRoleId",
                table: "Flights",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CrewRoleId",
                table: "FlightCrew",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LatestCrewRoleId",
                table: "CrewMembers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CrewRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ShortName = table.Column<string>(type: "varchar(6)", maxLength: 6, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsFlightDeck = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrewRoles", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "CrewRoles",
                columns: new[] { "Id", "IsFlightDeck", "Name", "ShortName", "SortOrder" },
                values: new object[,]
                {
                    { 1, true, "Captain", "CPT", 10 },
                    { 2, true, "First officer", "FO", 20 },
                    { 3, true, "Second officer", "SO", 30 },
                    { 4, false, "Cabin crew", "CC", 40 },
                    { 5, false, "Passive", "PAX", 50 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Flights_MyCrewRoleId",
                table: "Flights",
                column: "MyCrewRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_FlightCrew_CrewRoleId",
                table: "FlightCrew",
                column: "CrewRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_CrewMembers_LatestCrewRoleId",
                table: "CrewMembers",
                column: "LatestCrewRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_CrewRoles_Name",
                table: "CrewRoles",
                column: "Name",
                unique: true);

            // ---- Move existing role text into CrewRoles and link by id ----------------------
            // 1. Any role names not already seeded (case-insensitive match via the unique index).
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `CrewRoles` (`Name`, `ShortName`, `SortOrder`, `IsFlightDeck`)
                SELECT DISTINCT TRIM(`Role`), NULL, 100, FALSE FROM `FlightCrew`
                WHERE `Role` IS NOT NULL AND TRIM(`Role`) <> '';");
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `CrewRoles` (`Name`, `ShortName`, `SortOrder`, `IsFlightDeck`)
                SELECT DISTINCT TRIM(`LatestRole`), NULL, 100, FALSE FROM `CrewMembers`
                WHERE `LatestRole` IS NOT NULL AND TRIM(`LatestRole`) <> '';");
            // Flights.MyRole may hold logbook codes (CPT, PSV, …): map them to role names first.
            const string myRoleName = @"CASE UPPER(TRIM(`MyRole`))
                    WHEN 'CPT' THEN 'Captain' WHEN 'CAPT' THEN 'Captain' WHEN 'PIC' THEN 'Captain'
                    WHEN 'FO' THEN 'First officer' WHEN 'COP' THEN 'First officer' WHEN 'SIC' THEN 'First officer'
                    WHEN 'SO' THEN 'Second officer'
                    WHEN 'CC' THEN 'Cabin crew' WHEN 'CA' THEN 'Cabin crew'
                    WHEN 'PSV' THEN 'Passive' WHEN 'PAX' THEN 'Passive' WHEN 'DH' THEN 'Passive'
                    ELSE TRIM(`MyRole`) END";
            migrationBuilder.Sql($@"
                INSERT IGNORE INTO `CrewRoles` (`Name`, `ShortName`, `SortOrder`, `IsFlightDeck`)
                SELECT DISTINCT {myRoleName}, NULL, 100, FALSE FROM `Flights`
                WHERE `MyRole` IS NOT NULL AND TRIM(`MyRole`) <> '';");

            // 2. Point each row at its role.
            migrationBuilder.Sql(@"
                UPDATE `FlightCrew` fc JOIN `CrewRoles` r ON r.`Name` = TRIM(fc.`Role`)
                SET fc.`CrewRoleId` = r.`Id`;");
            migrationBuilder.Sql(@"
                UPDATE `CrewMembers` cm JOIN `CrewRoles` r ON r.`Name` = TRIM(cm.`LatestRole`)
                SET cm.`LatestCrewRoleId` = r.`Id`;");
            migrationBuilder.Sql($@"
                UPDATE `Flights` f JOIN `CrewRoles` r ON r.`Name` = ({myRoleName.Replace("`MyRole`", "f.`MyRole`")})
                SET f.`MyCrewRoleId` = r.`Id`;");

            // 3. The old text columns are no longer needed.
            migrationBuilder.DropColumn(
                name: "MyRole",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "FlightCrew");

            migrationBuilder.DropColumn(
                name: "LatestRole",
                table: "CrewMembers");

            migrationBuilder.AddForeignKey(
                name: "FK_CrewMembers_CrewRoles_LatestCrewRoleId",
                table: "CrewMembers",
                column: "LatestCrewRoleId",
                principalTable: "CrewRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FlightCrew_CrewRoles_CrewRoleId",
                table: "FlightCrew",
                column: "CrewRoleId",
                principalTable: "CrewRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Flights_CrewRoles_MyCrewRoleId",
                table: "Flights",
                column: "MyCrewRoleId",
                principalTable: "CrewRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CrewMembers_CrewRoles_LatestCrewRoleId",
                table: "CrewMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_FlightCrew_CrewRoles_CrewRoleId",
                table: "FlightCrew");

            migrationBuilder.DropForeignKey(
                name: "FK_Flights_CrewRoles_MyCrewRoleId",
                table: "Flights");

            migrationBuilder.DropTable(
                name: "CrewRoles");

            migrationBuilder.DropIndex(
                name: "IX_Flights_MyCrewRoleId",
                table: "Flights");

            migrationBuilder.DropIndex(
                name: "IX_FlightCrew_CrewRoleId",
                table: "FlightCrew");

            migrationBuilder.DropIndex(
                name: "IX_CrewMembers_LatestCrewRoleId",
                table: "CrewMembers");

            migrationBuilder.DropColumn(
                name: "MyCrewRoleId",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "CrewRoleId",
                table: "FlightCrew");

            migrationBuilder.DropColumn(
                name: "LatestCrewRoleId",
                table: "CrewMembers");

            migrationBuilder.AddColumn<string>(
                name: "MyRole",
                table: "Flights",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "FlightCrew",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LatestRole",
                table: "CrewMembers",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
