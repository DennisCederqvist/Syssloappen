using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syssloappen.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdDeletionScheduledAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionScheduledAt",
                table: "Households",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletionScheduledAt",
                table: "Households");
        }
    }
}
