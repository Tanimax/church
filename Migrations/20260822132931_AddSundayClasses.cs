using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ChurchAttendance.Migrations
{
    /// <inheritdoc />
    public partial class AddSundayClasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SundayClassId",
                table: "Members",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SundayClasses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SundayClasses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Members_SundayClassId",
                table: "Members",
                column: "SundayClassId");

            migrationBuilder.AddForeignKey(
                name: "FK_Members_SundayClasses_SundayClassId",
                table: "Members",
                column: "SundayClassId",
                principalTable: "SundayClasses",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Members_SundayClasses_SundayClassId",
                table: "Members");

            migrationBuilder.DropTable(
                name: "SundayClasses");

            migrationBuilder.DropIndex(
                name: "IX_Members_SundayClassId",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "SundayClassId",
                table: "Members");
        }
    }
}
