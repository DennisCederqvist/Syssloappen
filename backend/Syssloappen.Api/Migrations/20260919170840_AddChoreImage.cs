using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syssloappen.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddChoreImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Chores",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Chores");
        }
    }
}
