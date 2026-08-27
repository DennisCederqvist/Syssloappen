using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syssloappen.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRewardRedemptionDeliveryAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveredByUserId",
                table: "RewardRedemptions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RewardRedemptions_DeliveredByUserId",
                table: "RewardRedemptions",
                column: "DeliveredByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_RewardRedemptions_AspNetUsers_DeliveredByUserId",
                table: "RewardRedemptions",
                column: "DeliveredByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RewardRedemptions_AspNetUsers_DeliveredByUserId",
                table: "RewardRedemptions");

            migrationBuilder.DropIndex(
                name: "IX_RewardRedemptions_DeliveredByUserId",
                table: "RewardRedemptions");

            migrationBuilder.DropColumn(
                name: "DeliveredByUserId",
                table: "RewardRedemptions");
        }
    }
}
