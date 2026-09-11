using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syssloappen.Api.Migrations
{
    /// <summary>
    /// Every table in the `public` schema is also reachable through Supabase's PostgREST API
    /// (the same REST endpoint the `anon`/publishable key is meant to be used with), independent
    /// of and unauthenticated-by our own ASP.NET Core Identity/HouseholdId authorization. Without
    /// Row Level Security, Supabase's own linter flags this correctly: anyone holding just the
    /// publishable key could read or write every household's data directly via PostgREST,
    /// completely bypassing the app.
    ///
    /// This app never uses PostgREST — the API talks to Postgres directly via Npgsql/EF Core, as
    /// the table-owning role, which always bypasses RLS regardless of policies (Postgres RLS
    /// exempts superusers/owners by design). So enabling RLS with *no* policies defined is a
    /// pure deny-all for PostgREST's anon/authenticated roles, with zero effect on this app.
    /// </summary>
    public partial class EnableRowLevelSecurity : Migration
    {
        private static readonly string[] Tables =
        [
            "__EFMigrationsHistory",
            "Households",
            "AspNetRoles",
            "AspNetRoleClaims",
            "AspNetUserClaims",
            "AspNetUserLogins",
            "AspNetUserRoles",
            "AspNetUserTokens",
            "AspNetUsers",
            "ChildProfiles",
            "ChildPairingCodes",
            "ChildDeviceSessions",
            "ChildPointReservations",
            "Chores",
            "ChoreAssignments",
            "ChoreCompletions",
            "HouseholdInvitations",
            "Rewards",
            "RewardRedemptions",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"ALTER TABLE \"{table}\" ENABLE ROW LEVEL SECURITY;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"ALTER TABLE \"{table}\" DISABLE ROW LEVEL SECURITY;");
            }
        }
    }
}
