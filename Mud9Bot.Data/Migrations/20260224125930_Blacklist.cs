using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mud9Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class Blacklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "blacklist",
                columns: table => new
                {
                    telegram_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reason = table.Column<string>(type: "text", nullable: true),
                    banned_by = table.Column<long>(type: "bigint", nullable: false),
                    time_added = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blacklist", x => x.telegram_id);
                });

            migrationBuilder.UpdateData(
                table: "system_settings",
                keyColumn: "setting_key",
                keyValue: "broadcast_delay_ms",
                column: "last_updated",
                value: new DateTime(2026, 2, 24, 12, 59, 30, 563, DateTimeKind.Utc).AddTicks(9620));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blacklist");

            migrationBuilder.UpdateData(
                table: "system_settings",
                keyColumn: "setting_key",
                keyValue: "broadcast_delay_ms",
                column: "last_updated",
                value: new DateTime(2026, 2, 24, 9, 51, 31, 565, DateTimeKind.Utc).AddTicks(8530));
        }
    }
}
