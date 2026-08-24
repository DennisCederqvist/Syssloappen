using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syssloappen.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddChildDeviceSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChildDeviceSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<int>(type: "integer", nullable: false),
                    ChildProfileId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    SecretHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AbsoluteExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildDeviceSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChildDeviceSessions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChildDeviceSessions_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChildDeviceSessions_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChildDeviceSessions_ChildProfileId",
                table: "ChildDeviceSessions",
                column: "ChildProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildDeviceSessions_HouseholdId_ChildProfileId",
                table: "ChildDeviceSessions",
                columns: new[] { "HouseholdId", "ChildProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChildDeviceSessions_SecretHash",
                table: "ChildDeviceSessions",
                column: "SecretHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChildDeviceSessions_UserId",
                table: "ChildDeviceSessions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChildDeviceSessions");
        }
    }
}
