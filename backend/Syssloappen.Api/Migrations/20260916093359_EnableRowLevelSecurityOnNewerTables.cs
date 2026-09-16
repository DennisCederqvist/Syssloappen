using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Syssloappen.Api.Migrations
{
    /// <summary>
    /// The <see cref="EnableRowLevelSecurity"/> migration enabled RLS on every table that existed
    /// at the time, but two tables added afterwards (ChoreRecurrences, PushSubscriptions) were
    /// missed and left publicly readable/writable via Supabase's PostgREST API. This closes that
    /// gap the same way: enable RLS with no policies, which is a pure deny-all for PostgREST's
    /// anon/authenticated roles and has no effect on this app (it talks to Postgres directly via
    /// Npgsql/EF Core as the table-owning role, which always bypasses RLS).
    /// </summary>
    public partial class EnableRowLevelSecurityOnNewerTables : Migration
    {
        private static readonly string[] Tables =
        [
            "ChoreRecurrences",
            "PushSubscriptions",
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
