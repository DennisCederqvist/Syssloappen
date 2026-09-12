using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syssloappen.Api.Migrations
{
    /// <inheritdoc />
    public partial class AllowArbitraryChorePoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Chores_Points",
                table: "Chores");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChoreCompletions_PointsAwarded",
                table: "ChoreCompletions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChoreAssignments_Points",
                table: "ChoreAssignments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Chores_Points",
                table: "Chores",
                sql: "\"Points\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChoreCompletions_PointsAwarded",
                table: "ChoreCompletions",
                sql: "\"PointsAwarded\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChoreAssignments_Points",
                table: "ChoreAssignments",
                sql: "\"Points\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Chores_Points",
                table: "Chores");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChoreCompletions_PointsAwarded",
                table: "ChoreCompletions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChoreAssignments_Points",
                table: "ChoreAssignments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Chores_Points",
                table: "Chores",
                sql: "\"Points\" IN (5, 10, 15, 20)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChoreCompletions_PointsAwarded",
                table: "ChoreCompletions",
                sql: "\"PointsAwarded\" IN (5, 10, 15, 20)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChoreAssignments_Points",
                table: "ChoreAssignments",
                sql: "\"Points\" IN (5, 10, 15, 20)");
        }
    }
}
