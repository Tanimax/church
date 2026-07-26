using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurchAttendance.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Members",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FullName = table.Column<string>(type: "TEXT", nullable: false),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    PhotoUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Token = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Members", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Attendances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MemberId = table.Column<int>(type: "INTEGER", nullable: false),
                    ServiceSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    CheckedInAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CheckedInBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attendances_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Attendances_ServiceSessions_ServiceSessionId",
                        column: x => x.ServiceSessionId,
                        principalTable: "ServiceSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_MemberId_ServiceSessionId",
                table: "Attendances",
                columns: new[] { "MemberId", "ServiceSessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_ServiceSessionId",
                table: "Attendances",
                column: "ServiceSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Members_Token",
                table: "Members",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceSessions_Date",
                table: "ServiceSessions",
                column: "Date",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Attendances");

            migrationBuilder.DropTable(
                name: "Members");

            migrationBuilder.DropTable(
                name: "ServiceSessions");
        }
    }
}
