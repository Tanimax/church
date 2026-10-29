using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ChurchAttendance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Quarter = table.Column<int>(type: "integer", nullable: false),
                    FooterNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceSchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledServices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServiceScheduleId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Leader = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Preacher = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledServices_ServiceSchedules_ServiceScheduleId",
                        column: x => x.ServiceScheduleId,
                        principalTable: "ServiceSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledServices_ServiceScheduleId_Date",
                table: "ScheduledServices",
                columns: new[] { "ServiceScheduleId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceSchedules_Year_Quarter",
                table: "ServiceSchedules",
                columns: new[] { "Year", "Quarter" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledServices");

            migrationBuilder.DropTable(
                name: "ServiceSchedules");
        }
    }
}
