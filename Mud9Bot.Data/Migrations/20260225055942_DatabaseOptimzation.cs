using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mud9Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class DatabaseOptimzation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "system_settings",
                keyColumn: "setting_key",
                keyValue: "broadcast_delay_ms",
                column: "last_updated",
                value: new DateTime(2026, 2, 25, 5, 59, 42, 245, DateTimeKind.Utc).AddTicks(990));

            migrationBuilder.CreateIndex(
                name: "IX_wineplastic_groupid_disabled",
                table: "wineplastic",
                columns: new[] { "groupid", "disabled" });

            migrationBuilder.CreateIndex(
                name: "IX_wineplastic_userid",
                table: "wineplastic",
                column: "userid");

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                table: "users",
                column: "username");

            migrationBuilder.CreateIndex(
                name: "IX_donation_time",
                table: "donation",
                column: "time");

            migrationBuilder.CreateIndex(
                name: "IX_command_logs_Timestamp",
                table: "command_logs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_bot_event_logs_count",
                table: "bot_event_logs",
                column: "count");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_wineplastic_groupid_disabled",
                table: "wineplastic");

            migrationBuilder.DropIndex(
                name: "IX_wineplastic_userid",
                table: "wineplastic");

            migrationBuilder.DropIndex(
                name: "IX_users_username",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_donation_time",
                table: "donation");

            migrationBuilder.DropIndex(
                name: "IX_command_logs_Timestamp",
                table: "command_logs");

            migrationBuilder.DropIndex(
                name: "IX_bot_event_logs_count",
                table: "bot_event_logs");

            migrationBuilder.UpdateData(
                table: "system_settings",
                keyColumn: "setting_key",
                keyValue: "broadcast_delay_ms",
                column: "last_updated",
                value: new DateTime(2026, 2, 24, 15, 51, 46, 790, DateTimeKind.Utc).AddTicks(6130));
        }
    }
}
