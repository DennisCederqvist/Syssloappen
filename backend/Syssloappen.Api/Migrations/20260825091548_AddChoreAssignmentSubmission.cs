using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syssloappen.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddChoreAssignmentSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ChoreAssignments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Assigned");

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "ChoreAssignments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "ChoreAssignments");
        }
    }
}
