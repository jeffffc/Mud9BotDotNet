using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Mud9Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemSettings2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "system_settings",
                keyColumn: "setting_key",
                keyValue: "is_maintenance",
                column: "last_updated",
                value: new DateTime(2026, 2, 24, 8, 42, 52, 968, DateTimeKind.Utc).AddTicks(5860));

            migrationBuilder.UpdateData(
                table: "system_settings",
                keyColumn: "setting_key",
                keyValue: "maintenance_message",
                column: "last_updated",
                value: new DateTime(2026, 2, 24, 8, 42, 52, 968, DateTimeKind.Utc).AddTicks(5990));

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "setting_key", "description", "last_updated", "setting_value" },
                values: new object[,]
                {
                    { "broadcast_delay_ms", "Delay between messages during global broadcast (ms)", new DateTime(2026, 2, 24, 8, 42, 52, 968, DateTimeKind.Utc).AddTicks(5990), "35" },
                    { "enable_gas", "Feature flag: Enable gas price service", new DateTime(2026, 2, 24, 8, 42, 52, 968, DateTimeKind.Utc).AddTicks(6000), "true" },
                    { "enable_wineplastic", "Feature flag: Enable core wine/plastic interactions", new DateTime(2026, 2, 24, 8, 42, 52, 968, DateTimeKind.Utc).AddTicks(6000), "true" },
                    { "enable_zodiac", "Feature flag: Enable daily zodiac horoscopes", new DateTime(2026, 2, 24, 8, 42, 52, 968, DateTimeKind.Utc).AddTicks(6000), "true" },
                    { "web_banner_message", "Site-wide announcement message for the web dashboard", new DateTime(2026, 2, 24, 8, 42, 52, 968, DateTimeKind.Utc).AddTicks(6000), "" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "setting_key",
                keyValue: "broadcast_delay_ms");

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "setting_key",
                keyValue: "enable_gas");

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "setting_key",
                keyValue: "enable_wineplastic");

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "setting_key",
                keyValue: "enable_zodiac");

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "setting_key",
                keyValue: "web_banner_message");

            migrationBuilder.UpdateData(
                table: "system_settings",
                keyColumn: "setting_key",
                keyValue: "is_maintenance",
                column: "last_updated",
                value: new DateTime(2026, 2, 24, 8, 25, 4, 610, DateTimeKind.Utc).AddTicks(4960));

            migrationBuilder.UpdateData(
                table: "system_settings",
                keyColumn: "setting_key",
                keyValue: "maintenance_message",
                column: "last_updated",
                value: new DateTime(2026, 2, 24, 8, 25, 4, 610, DateTimeKind.Utc).AddTicks(5090));
        }
    }
}
