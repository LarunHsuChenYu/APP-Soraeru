using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Soraeru.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmRuntimeSystemPrompts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MeaningReadingOnlySystemPrompt",
                table: "LlmRuntimeSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemPrompt",
                table: "LlmRuntimeSettings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MeaningReadingOnlySystemPrompt",
                table: "LlmRuntimeSettings");

            migrationBuilder.DropColumn(
                name: "SystemPrompt",
                table: "LlmRuntimeSettings");
        }
    }
}
