using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Verdure.Mcp.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessageUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "user_id",
                table: "chat_messages",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_user_id",
                table: "chat_messages",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_user_id_chat_room_id_created_at",
                table: "chat_messages",
                columns: new[] { "user_id", "chat_room_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_chat_messages_user_id",
                table: "chat_messages");

            migrationBuilder.DropIndex(
                name: "IX_chat_messages_user_id_chat_room_id_created_at",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "chat_messages");
        }
    }
}
