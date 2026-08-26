using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syssloappen.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddChoreAssignmentCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "ChoreAssignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledByUserId",
                table: "ChoreAssignments",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChoreAssignments_CancelledByUserId",
                table: "ChoreAssignments",
                column: "CancelledByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChoreAssignments_AspNetUsers_CancelledByUserId",
                table: "ChoreAssignments",
                column: "CancelledByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChoreAssignments_AspNetUsers_CancelledByUserId",
                table: "ChoreAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ChoreAssignments_CancelledByUserId",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "CancelledByUserId",
                table: "ChoreAssignments");
        }
    }
}
