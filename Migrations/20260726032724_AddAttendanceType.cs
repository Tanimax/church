using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurchAttendance.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Attendances_MemberId_ServiceSessionId",
                table: "Attendances");

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Attendances",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_MemberId_ServiceSessionId_Type",
                table: "Attendances",
                columns: new[] { "MemberId", "ServiceSessionId", "Type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Attendances_MemberId_ServiceSessionId_Type",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Attendances");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_MemberId_ServiceSessionId",
                table: "Attendances",
                columns: new[] { "MemberId", "ServiceSessionId" },
                unique: true);
        }
    }
}
