using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syssloappen.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddChoreAssignmentAdultArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AdultArchivedAt",
                table: "ChoreAssignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChoreAssignments_HouseholdId_AdultArchivedAt",
                table: "ChoreAssignments",
                columns: new[] { "HouseholdId", "AdultArchivedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChoreAssignments_HouseholdId_AdultArchivedAt",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "AdultArchivedAt",
                table: "ChoreAssignments");
        }
    }
}
