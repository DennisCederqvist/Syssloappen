using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syssloappen.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddChoreAssignmentDueDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChoreAssignments_HouseholdId_ChildId",
                table: "ChoreAssignments");

            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                table: "ChoreAssignments",
                type: "date",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"ChoreAssignments\" SET \"DueDate\" = (\"AssignedAt\" AT TIME ZONE 'UTC')::date;");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "DueDate",
                table: "ChoreAssignments",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChoreAssignments_HouseholdId_ChildId_DueDate",
                table: "ChoreAssignments",
                columns: new[] { "HouseholdId", "ChildId", "DueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChoreAssignments_HouseholdId_ChildId_DueDate",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "ChoreAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreAssignments_HouseholdId_ChildId",
                table: "ChoreAssignments",
                columns: new[] { "HouseholdId", "ChildId" });
        }
    }
}
