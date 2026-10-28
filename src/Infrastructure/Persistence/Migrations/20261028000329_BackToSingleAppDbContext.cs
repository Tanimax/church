using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurchAttendance.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Undoes the Identity module split: the Users table goes back into AppDbContext's model,
    /// the only DbContext again. The table itself never moved (MoveUsersToIdentityModule was
    /// empty too), so there is nothing to create; this only drops the separate migration
    /// history table IdentityDbContext had created, if it exists.
    /// </summary>
    public partial class BackToSingleAppDbContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "__EFMigrationsHistory_Identity";""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
