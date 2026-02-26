using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mud9Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class BusETA3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BusRouteStop_BusRoute_RouteId",
                table: "BusRouteStop");

            migrationBuilder.DropForeignKey(
                name: "FK_BusRouteStop_BusStop_StopId",
                table: "BusRouteStop");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BusStop",
                table: "BusStop");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BusRouteStop",
                table: "BusRouteStop");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BusRoute",
                table: "BusRoute");

            migrationBuilder.RenameTable(
                name: "BusStop",
                newName: "bus_stops");

            migrationBuilder.RenameTable(
                name: "BusRouteStop",
                newName: "bus_route_stops");

            migrationBuilder.RenameTable(
                name: "BusRoute",
                newName: "bus_routes");

            migrationBuilder.RenameColumn(
                name: "Longitude",
                table: "bus_stops",
                newName: "longitude");

            migrationBuilder.RenameColumn(
                name: "Latitude",
                table: "bus_stops",
                newName: "latitude");

            migrationBuilder.RenameColumn(
                name: "NameTc",
                table: "bus_stops",
                newName: "name_tc");

            migrationBuilder.RenameColumn(
                name: "NameEn",
                table: "bus_stops",
                newName: "name_en");

            migrationBuilder.RenameColumn(
                name: "LastUpdated",
                table: "bus_stops",
                newName: "last_updated");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "bus_stops",
                newName: "is_active");

            migrationBuilder.RenameColumn(
                name: "StopId",
                table: "bus_stops",
                newName: "stop_id");

            migrationBuilder.RenameColumn(
                name: "Sequence",
                table: "bus_route_stops",
                newName: "sequence");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "bus_route_stops",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "StopId",
                table: "bus_route_stops",
                newName: "stop_id");

            migrationBuilder.RenameColumn(
                name: "RouteId",
                table: "bus_route_stops",
                newName: "route_id");

            migrationBuilder.RenameColumn(
                name: "LastUpdated",
                table: "bus_route_stops",
                newName: "last_updated");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "bus_route_stops",
                newName: "is_active");

            migrationBuilder.RenameIndex(
                name: "IX_BusRouteStop_StopId",
                table: "bus_route_stops",
                newName: "IX_bus_route_stops_stop_id");

            migrationBuilder.RenameIndex(
                name: "IX_BusRouteStop_RouteId",
                table: "bus_route_stops",
                newName: "IX_bus_route_stops_route_id");

            migrationBuilder.RenameColumn(
                name: "Company",
                table: "bus_routes",
                newName: "company");

            migrationBuilder.RenameColumn(
                name: "Bound",
                table: "bus_routes",
                newName: "bound");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "bus_routes",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "ServiceType",
                table: "bus_routes",
                newName: "service_type");

            migrationBuilder.RenameColumn(
                name: "RouteNumber",
                table: "bus_routes",
                newName: "route_number");

            migrationBuilder.RenameColumn(
                name: "OriginTc",
                table: "bus_routes",
                newName: "origin_tc");

            migrationBuilder.RenameColumn(
                name: "OriginEn",
                table: "bus_routes",
                newName: "origin_en");

            migrationBuilder.RenameColumn(
                name: "LastUpdated",
                table: "bus_routes",
                newName: "last_updated");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "bus_routes",
                newName: "is_active");

            migrationBuilder.RenameColumn(
                name: "DestinationTc",
                table: "bus_routes",
                newName: "destination_tc");

            migrationBuilder.RenameColumn(
                name: "DestinationEn",
                table: "bus_routes",
                newName: "destination_en");

            migrationBuilder.AddPrimaryKey(
                name: "PK_bus_stops",
                table: "bus_stops",
                column: "stop_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_bus_route_stops",
                table: "bus_route_stops",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_bus_routes",
                table: "bus_routes",
                column: "id");

            migrationBuilder.UpdateData(
                table: "system_settings",
                keyColumn: "setting_key",
                keyValue: "broadcast_delay_ms",
                column: "last_updated",
                value: new DateTime(2026, 2, 25, 18, 21, 35, 517, DateTimeKind.Utc).AddTicks(4790));

            migrationBuilder.AddForeignKey(
                name: "FK_bus_route_stops_bus_routes_route_id",
                table: "bus_route_stops",
                column: "route_id",
                principalTable: "bus_routes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_bus_route_stops_bus_stops_stop_id",
                table: "bus_route_stops",
                column: "stop_id",
                principalTable: "bus_stops",
                principalColumn: "stop_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bus_route_stops_bus_routes_route_id",
                table: "bus_route_stops");

            migrationBuilder.DropForeignKey(
                name: "FK_bus_route_stops_bus_stops_stop_id",
                table: "bus_route_stops");

            migrationBuilder.DropPrimaryKey(
                name: "PK_bus_stops",
                table: "bus_stops");

            migrationBuilder.DropPrimaryKey(
                name: "PK_bus_routes",
                table: "bus_routes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_bus_route_stops",
                table: "bus_route_stops");

            migrationBuilder.RenameTable(
                name: "bus_stops",
                newName: "BusStop");

            migrationBuilder.RenameTable(
                name: "bus_routes",
                newName: "BusRoute");

            migrationBuilder.RenameTable(
                name: "bus_route_stops",
                newName: "BusRouteStop");

            migrationBuilder.RenameColumn(
                name: "longitude",
                table: "BusStop",
                newName: "Longitude");

            migrationBuilder.RenameColumn(
                name: "latitude",
                table: "BusStop",
                newName: "Latitude");

            migrationBuilder.RenameColumn(
                name: "name_tc",
                table: "BusStop",
                newName: "NameTc");

            migrationBuilder.RenameColumn(
                name: "name_en",
                table: "BusStop",
                newName: "NameEn");

            migrationBuilder.RenameColumn(
                name: "last_updated",
                table: "BusStop",
                newName: "LastUpdated");

            migrationBuilder.RenameColumn(
                name: "is_active",
                table: "BusStop",
                newName: "IsActive");

            migrationBuilder.RenameColumn(
                name: "stop_id",
                table: "BusStop",
                newName: "StopId");

            migrationBuilder.RenameColumn(
                name: "company",
                table: "BusRoute",
                newName: "Company");

            migrationBuilder.RenameColumn(
                name: "bound",
                table: "BusRoute",
                newName: "Bound");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "BusRoute",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "service_type",
                table: "BusRoute",
                newName: "ServiceType");

            migrationBuilder.RenameColumn(
                name: "route_number",
                table: "BusRoute",
                newName: "RouteNumber");

            migrationBuilder.RenameColumn(
                name: "origin_tc",
                table: "BusRoute",
                newName: "OriginTc");

            migrationBuilder.RenameColumn(
                name: "origin_en",
                table: "BusRoute",
                newName: "OriginEn");

            migrationBuilder.RenameColumn(
                name: "last_updated",
                table: "BusRoute",
                newName: "LastUpdated");

            migrationBuilder.RenameColumn(
                name: "is_active",
                table: "BusRoute",
                newName: "IsActive");

            migrationBuilder.RenameColumn(
                name: "destination_tc",
                table: "BusRoute",
                newName: "DestinationTc");

            migrationBuilder.RenameColumn(
                name: "destination_en",
                table: "BusRoute",
                newName: "DestinationEn");

            migrationBuilder.RenameColumn(
                name: "sequence",
                table: "BusRouteStop",
                newName: "Sequence");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "BusRouteStop",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "stop_id",
                table: "BusRouteStop",
                newName: "StopId");

            migrationBuilder.RenameColumn(
                name: "route_id",
                table: "BusRouteStop",
                newName: "RouteId");

            migrationBuilder.RenameColumn(
                name: "last_updated",
                table: "BusRouteStop",
                newName: "LastUpdated");

            migrationBuilder.RenameColumn(
                name: "is_active",
                table: "BusRouteStop",
                newName: "IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_bus_route_stops_stop_id",
                table: "BusRouteStop",
                newName: "IX_BusRouteStop_StopId");

            migrationBuilder.RenameIndex(
                name: "IX_bus_route_stops_route_id",
                table: "BusRouteStop",
                newName: "IX_BusRouteStop_RouteId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BusStop",
                table: "BusStop",
                column: "StopId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BusRoute",
                table: "BusRoute",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BusRouteStop",
                table: "BusRouteStop",
                column: "Id");

            migrationBuilder.UpdateData(
                table: "system_settings",
                keyColumn: "setting_key",
                keyValue: "broadcast_delay_ms",
                column: "last_updated",
                value: new DateTime(2026, 2, 25, 18, 18, 26, 991, DateTimeKind.Utc).AddTicks(850));

            migrationBuilder.AddForeignKey(
                name: "FK_BusRouteStop_BusRoute_RouteId",
                table: "BusRouteStop",
                column: "RouteId",
                principalTable: "BusRoute",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BusRouteStop_BusStop_StopId",
                table: "BusRouteStop",
                column: "StopId",
                principalTable: "BusStop",
                principalColumn: "StopId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
