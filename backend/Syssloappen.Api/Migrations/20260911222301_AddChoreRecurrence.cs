using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Syssloappen.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddChoreRecurrence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GeneratedFromRecurrenceId",
                table: "ChoreAssignments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChoreRecurrences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HouseholdId = table.Column<int>(type: "integer", nullable: false),
                    ChoreId = table.Column<int>(type: "integer", nullable: false),
                    ChildId = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: false),
                    Frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DaysOfWeekMask = table.Column<int>(type: "integer", nullable: true),
                    DayOfMonth = table.Column<int>(type: "integer", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChoreRecurrences", x => x.Id);
                    table.CheckConstraint("CK_ChoreRecurrences_DayOfMonth", "\"DayOfMonth\" IS NULL OR (\"DayOfMonth\" BETWEEN 1 AND 31)");
                    table.CheckConstraint("CK_ChoreRecurrences_DaysOfWeekMask", "\"DaysOfWeekMask\" IS NULL OR (\"DaysOfWeekMask\" BETWEEN 1 AND 127)");
                    table.ForeignKey(
                        name: "FK_ChoreRecurrences_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChoreRecurrences_ChildProfiles_ChildId",
                        column: x => x.ChildId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChoreRecurrences_Chores_ChoreId",
                        column: x => x.ChoreId,
                        principalTable: "Chores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChoreRecurrences_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChoreAssignments_GeneratedFromRecurrenceId_DueDate",
                table: "ChoreAssignments",
                columns: new[] { "GeneratedFromRecurrenceId", "DueDate" },
                unique: true,
                filter: "\"GeneratedFromRecurrenceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreRecurrences_ChildId",
                table: "ChoreRecurrences",
                column: "ChildId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreRecurrences_ChoreId",
                table: "ChoreRecurrences",
                column: "ChoreId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreRecurrences_CreatedByUserId",
                table: "ChoreRecurrences",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreRecurrences_HouseholdId_IsActive",
                table: "ChoreRecurrences",
                columns: new[] { "HouseholdId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_ChoreAssignments_ChoreRecurrences_GeneratedFromRecurrenceId",
                table: "ChoreAssignments",
                column: "GeneratedFromRecurrenceId",
                principalTable: "ChoreRecurrences",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChoreAssignments_ChoreRecurrences_GeneratedFromRecurrenceId",
                table: "ChoreAssignments");

            migrationBuilder.DropTable(
                name: "ChoreRecurrences");

            migrationBuilder.DropIndex(
                name: "IX_ChoreAssignments_GeneratedFromRecurrenceId_DueDate",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "GeneratedFromRecurrenceId",
                table: "ChoreAssignments");
        }
    }
}
