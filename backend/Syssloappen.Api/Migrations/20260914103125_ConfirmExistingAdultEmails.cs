using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syssloappen.Api.Migrations
{
    /// <inheritdoc />
    public partial class ConfirmExistingAdultEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Email confirmation on registration ships after these accounts already exist.
            // Grandfather in every existing Adult (Child rows have no email and are unaffected)
            // so nobody who could already log in gets locked out; only new signups from here on
            // must confirm.
            migrationBuilder.Sql(
                """
                UPDATE "AspNetUsers"
                SET "EmailConfirmed" = TRUE
                WHERE "Email" IS NOT NULL AND "EmailConfirmed" = FALSE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately irreversible: we cannot know which of these accounts were confirmed
            // via this backfill versus a real confirmation click, so reverting would incorrectly
            // un-confirm real confirmations too.
        }
    }
}
