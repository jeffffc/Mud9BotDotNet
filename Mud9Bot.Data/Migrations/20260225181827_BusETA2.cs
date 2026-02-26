using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mud9Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class BusETA2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusRoute",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Company = table.Column<string>(type: "text", nullable: false),
                    RouteNumber = table.Column<string>(type: "text", nullable: false),
                    Bound = table.Column<string>(type: "text", nullable: false),
                    ServiceType = table.Column<string>(type: "text", nullable: false),
                    OriginTc = table.Column<string>(type: "text", nullable: false),
                    OriginEn = table.Column<string>(type: "text", nullable: false),
                    DestinationTc = table.Column<string>(type: "text", nullable: false),
                    DestinationEn = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusRoute", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BusStop",
                columns: table => new
                {
                    StopId = table.Column<string>(type: "text", nullable: false),
                    NameTc = table.Column<string>(type: "text", nullable: false),
                    NameEn = table.Column<string>(type: "text", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusStop", x => x.StopId);
                });

            migrationBuilder.CreateTable(
                name: "BusRouteStop",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    RouteId = table.Column<string>(type: "text", nullable: false),
                    StopId = table.Column<string>(type: "text", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusRouteStop", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusRouteStop_BusRoute_RouteId",
                        column: x => x.RouteId,
                        principalTable: "BusRoute",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusRouteStop_BusStop_StopId",
                        column: x => x.StopId,
                        principalTable: "BusStop",
                        principalColumn: "StopId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "system_settings",
                keyColumn: "setting_key",
                keyValue: "broadcast_delay_ms",
                column: "last_updated",
                value: new DateTime(2026, 2, 25, 18, 18, 26, 991, DateTimeKind.Utc).AddTicks(850));

            migrationBuilder.CreateIndex(
                name: "IX_BusRouteStop_RouteId",
                table: "BusRouteStop",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_BusRouteStop_StopId",
                table: "BusRouteStop",
                column: "StopId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusRouteStop");

            migrationBuilder.DropTable(
                name: "BusRoute");

            migrationBuilder.DropTable(
                name: "BusStop");

            migrationBuilder.UpdateData(
                table: "system_settings",
                keyColumn: "setting_key",
                keyValue: "broadcast_delay_ms",
                column: "last_updated",
                value: new DateTime(2026, 2, 25, 17, 31, 3, 114, DateTimeKind.Utc).AddTicks(1190));
        }
    }
}
