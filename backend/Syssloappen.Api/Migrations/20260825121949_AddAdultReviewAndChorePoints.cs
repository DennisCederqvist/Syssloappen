using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Syssloappen.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAdultReviewAndChorePoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Points",
                table: "Chores",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "Points",
                table: "ChoreAssignments",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<string>(
                name: "ReviewComment",
                table: "ChoreAssignments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "ChoreAssignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByUserId",
                table: "ChoreAssignments",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChoreCompletions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HouseholdId = table.Column<int>(type: "integer", nullable: false),
                    AssignmentId = table.Column<int>(type: "integer", nullable: false),
                    ChildId = table.Column<int>(type: "integer", nullable: false),
                    ChoreId = table.Column<int>(type: "integer", nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "text", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PointsAwarded = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChoreCompletions", x => x.Id);
                    table.CheckConstraint("CK_ChoreCompletions_PointsAwarded", "\"PointsAwarded\" IN (5, 10, 15, 20)");
                    table.ForeignKey(
                        name: "FK_ChoreCompletions_AspNetUsers_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChoreCompletions_ChildProfiles_ChildId",
                        column: x => x.ChildId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChoreCompletions_ChoreAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "ChoreAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChoreCompletions_Chores_ChoreId",
                        column: x => x.ChoreId,
                        principalTable: "Chores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChoreCompletions_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Chores_Points",
                table: "Chores",
                sql: "\"Points\" IN (5, 10, 15, 20)");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreAssignments_ReviewedByUserId",
                table: "ChoreAssignments",
                column: "ReviewedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChoreAssignments_Points",
                table: "ChoreAssignments",
                sql: "\"Points\" IN (5, 10, 15, 20)");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreCompletions_ApprovedByUserId",
                table: "ChoreCompletions",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreCompletions_AssignmentId",
                table: "ChoreCompletions",
                column: "AssignmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChoreCompletions_ChildId",
                table: "ChoreCompletions",
                column: "ChildId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreCompletions_ChoreId",
                table: "ChoreCompletions",
                column: "ChoreId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreCompletions_HouseholdId_ChildId",
                table: "ChoreCompletions",
                columns: new[] { "HouseholdId", "ChildId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ChoreAssignments_AspNetUsers_ReviewedByUserId",
                table: "ChoreAssignments",
                column: "ReviewedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChoreAssignments_AspNetUsers_ReviewedByUserId",
                table: "ChoreAssignments");

            migrationBuilder.DropTable(
                name: "ChoreCompletions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Chores_Points",
                table: "Chores");

            migrationBuilder.DropIndex(
                name: "IX_ChoreAssignments_ReviewedByUserId",
                table: "ChoreAssignments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChoreAssignments_Points",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "Points",
                table: "Chores");

            migrationBuilder.DropColumn(
                name: "Points",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "ReviewComment",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "ChoreAssignments");
        }
    }
}
