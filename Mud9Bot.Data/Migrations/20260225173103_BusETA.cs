using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mud9Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class BusETA : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "system_settings",
                keyColumn: "setting_key",
                keyValue: "broadcast_delay_ms",
                column: "last_updated",
                value: new DateTime(2026, 2, 25, 17, 31, 3, 114, DateTimeKind.Utc).AddTicks(1190));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "system_settings",
                keyColumn: "setting_key",
                keyValue: "broadcast_delay_ms",
                column: "last_updated",
                value: new DateTime(2026, 2, 25, 6, 7, 4, 283, DateTimeKind.Utc).AddTicks(4030));
        }
    }
}
