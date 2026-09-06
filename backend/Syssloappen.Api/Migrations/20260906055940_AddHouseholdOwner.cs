using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syssloappen.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "Households",
                type: "text",
                nullable: true);

            // Every Adult account today came from either POST /api/auth/register
            // (which creates a brand-new Household) or POST /api/auth/register/invited
            // (which requires consuming an existing HouseholdInvitation). So a
            // Household with any invitation on record was, by construction, created by
            // whoever created that Household's earliest invitation — the only other
            // adult(s) could only have joined afterward, via that invitation or a later
            // one. This identifies the true historical owner, not a guess.
            migrationBuilder.Sql(
                """
                UPDATE "Households" h
                SET "OwnerUserId" = earliest."CreatedByUserId"
                FROM (
                    SELECT DISTINCT ON ("HouseholdId") "HouseholdId", "CreatedByUserId"
                    FROM "HouseholdInvitations"
                    ORDER BY "HouseholdId", "CreatedAt" ASC
                ) AS earliest
                WHERE h."Id" = earliest."HouseholdId";
                """);

            // A Household with no invitation on record has never had a second Adult
            // join, so its sole Adult is unambiguously the owner. The lexicographic
            // tie-break only matters for the never-expected case of a multi-adult
            // Household with no invitation history at all.
            migrationBuilder.Sql(
                """
                UPDATE "Households" h
                SET "OwnerUserId" = sole_adult."Id"
                FROM (
                    SELECT DISTINCT ON (u."HouseholdId") u."HouseholdId", u."Id"
                    FROM "AspNetUsers" u
                    JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
                    JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
                    WHERE r."Name" = 'Adult'
                    ORDER BY u."HouseholdId", u."Id" ASC
                ) AS sole_adult
                WHERE h."Id" = sole_adult."HouseholdId" AND h."OwnerUserId" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Households_OwnerUserId",
                table: "Households",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Households_AspNetUsers_OwnerUserId",
                table: "Households",
                column: "OwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Households_AspNetUsers_OwnerUserId",
                table: "Households");

            migrationBuilder.DropIndex(
                name: "IX_Households_OwnerUserId",
                table: "Households");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Households");
        }
    }
}
