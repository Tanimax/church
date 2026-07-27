using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurchAttendance.Migrations
{
    /// <inheritdoc />
    public partial class SplitMemberNameAddMaritalStatusAndBirthday : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FullName",
                table: "Members",
                newName: "LastName");

            migrationBuilder.AddColumn<int>(
                name: "BirthDay",
                table: "Members",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BirthMonth",
                table: "Members",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Members",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "MaritalStatus",
                table: "Members",
                type: "INTEGER",
                nullable: true);

            // The rename above moved the old FullName value ("Prénom Nom") wholesale into
            // LastName. Split it into the two new columns on the first space so existing
            // members keep readable names instead of an empty FirstName.
            migrationBuilder.Sql("""
                UPDATE Members
                SET FirstName = CASE WHEN instr(LastName, ' ') > 0 THEN substr(LastName, 1, instr(LastName, ' ') - 1) ELSE LastName END,
                    LastName = CASE WHEN instr(LastName, ' ') > 0 THEN substr(LastName, instr(LastName, ' ') + 1) ELSE '' END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recombine FirstName + LastName back into LastName before it gets renamed to
            // FullName, so the reverse migration doesn't drop the first name silently.
            migrationBuilder.Sql("""
                UPDATE Members
                SET LastName = CASE WHEN FirstName <> '' THEN FirstName || ' ' || LastName ELSE LastName END;
                """);

            migrationBuilder.DropColumn(
                name: "BirthDay",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "BirthMonth",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "MaritalStatus",
                table: "Members");

            migrationBuilder.RenameColumn(
                name: "LastName",
                table: "Members",
                newName: "FullName");
        }
    }
}
