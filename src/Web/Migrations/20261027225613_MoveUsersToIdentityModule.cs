using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurchAttendance.Migrations
{
    /// <summary>
    /// Hands the Users table over to the Identity module (IdentityDbContext). Intentionally
    /// empty: the table stays exactly where it is; this migration only removes User from
    /// AppDbContext's model snapshot so a future AppDbContext migration doesn't drop it.
    /// </summary>
    public partial class MoveUsersToIdentityModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
