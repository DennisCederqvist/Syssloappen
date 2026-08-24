using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syssloappen.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdFamilyCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FamilyCodeHash",
                table: "Households",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FamilyCodeLastFour",
                table: "Households",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FamilyCodeUpdatedAt",
                table: "Households",
                type: "timestamp with time zone",
                nullable: true);

            // Existing Households receive unique, deliberately unusable placeholders.
            // FamilyCodeLastFour remains null, so fallback login stays disabled until
            // an Adult rotates and receives a real code once after the migration.
            migrationBuilder.Sql(
                """
                UPDATE "Households"
                SET "FamilyCodeHash" = upper(encode(sha256(convert_to(
                    lpad(to_hex("Id"::bigint), 12, '0'), 'UTF8')), 'hex'))
                WHERE "FamilyCodeHash" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "FamilyCodeHash",
                table: "Households",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Households_FamilyCodeHash",
                table: "Households",
                column: "FamilyCodeHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Households_FamilyCodeHash",
                table: "Households");

            migrationBuilder.DropColumn(
                name: "FamilyCodeHash",
                table: "Households");

            migrationBuilder.DropColumn(
                name: "FamilyCodeLastFour",
                table: "Households");

            migrationBuilder.DropColumn(
                name: "FamilyCodeUpdatedAt",
                table: "Households");
        }
    }
}
