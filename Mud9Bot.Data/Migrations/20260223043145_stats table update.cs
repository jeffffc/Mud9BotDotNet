using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mud9Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class statstableupdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Metadata",
                table: "bot_event_logs",
                newName: "metadata");

            migrationBuilder.RenameColumn(
                name: "Count",
                table: "bot_event_logs",
                newName: "count");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "bot_event_logs",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "EventType",
                table: "bot_event_logs",
                newName: "event_type");

            migrationBuilder.RenameColumn(
                name: "ChatType",
                table: "bot_event_logs",
                newName: "chat_type");

            migrationBuilder.RenameIndex(
                name: "IX_bot_event_logs_EventType_Metadata_ChatType",
                table: "bot_event_logs",
                newName: "IX_bot_event_logs_event_type_metadata_chat_type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "metadata",
                table: "bot_event_logs",
                newName: "Metadata");

            migrationBuilder.RenameColumn(
                name: "count",
                table: "bot_event_logs",
                newName: "Count");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "bot_event_logs",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "event_type",
                table: "bot_event_logs",
                newName: "EventType");

            migrationBuilder.RenameColumn(
                name: "chat_type",
                table: "bot_event_logs",
                newName: "ChatType");

            migrationBuilder.RenameIndex(
                name: "IX_bot_event_logs_event_type_metadata_chat_type",
                table: "bot_event_logs",
                newName: "IX_bot_event_logs_EventType_Metadata_ChatType");
        }
    }
}
