using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurchAttendance.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberGenderAndBaptism : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Gender",
                table: "Members",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBaptized",
                table: "Members",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "IsBaptized",
                table: "Members");
        }
    }
}
