using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Verdure.Mcp.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentVoiceName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "voice_name",
                table: "agent_profiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "voice_name",
                table: "agent_profiles");
        }
    }
}
