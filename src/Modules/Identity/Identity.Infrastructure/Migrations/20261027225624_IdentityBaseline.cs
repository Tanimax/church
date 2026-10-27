using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurchAttendance.Identity.Infrastructure.Migrations
{
    /// <summary>
    /// Baseline for IdentityDbContext. Intentionally empty: the Users table it maps already
    /// exists (created by AppDbContext's AddUsers migration, on every database, old or new).
    /// Later Identity migrations build on this snapshot.
    /// </summary>
    public partial class IdentityBaseline : Migration
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
