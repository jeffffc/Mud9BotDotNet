using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Mud9Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    setting_key = table.Column<string>(type: "text", nullable: false),
                    setting_value = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    last_updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_settings", x => x.setting_key);
                });

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "setting_key", "description", "last_updated", "setting_value" },
                values: new object[,]
                {
                    { "broadcast_delay_ms", "Delay between messages during global broadcast (ms)", new DateTime(2026, 2, 24, 9, 51, 31, 565, DateTimeKind.Utc).AddTicks(8530), "35" },
                    { "enable_gas", "Feature flag: Enable gas price service", new DateTime(2026, 2, 24, 0, 0, 0, 0, DateTimeKind.Utc), "true" },
                    { "enable_wineplastic", "Feature flag: Enable core wine/plastic interactions", new DateTime(2026, 2, 24, 0, 0, 0, 0, DateTimeKind.Utc), "true" },
                    { "enable_zodiac", "Feature flag: Enable daily zodiac horoscopes", new DateTime(2026, 2, 24, 0, 0, 0, 0, DateTimeKind.Utc), "true" },
                    { "is_maintenance", "Toggle global maintenance mode", new DateTime(2026, 2, 24, 0, 0, 0, 0, DateTimeKind.Utc), "false" },
                    { "maintenance_message", "Message shown to users during maintenance", new DateTime(2026, 2, 24, 0, 0, 0, 0, DateTimeKind.Utc), "🛠 系統正在維護中，請稍後再試。 / System is under maintenance. Please try again later." },
                    { "web_banner_message", "Site-wide announcement message for the web dashboard", new DateTime(2026, 2, 24, 0, 0, 0, 0, DateTimeKind.Utc), "" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_settings");
        }
    }
}
