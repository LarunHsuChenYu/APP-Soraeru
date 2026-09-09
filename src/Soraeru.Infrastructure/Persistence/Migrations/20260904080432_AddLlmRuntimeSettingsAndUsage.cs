using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Soraeru.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmRuntimeSettingsAndUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LlmRuntimeSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApiKey = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Model = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedByEmail = table.Column<string>(type: "TEXT", maxLength: 320, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmRuntimeSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LlmUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FeatureType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PromptTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    CompletionTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    LatencyMs = table.Column<int>(type: "INTEGER", nullable: false),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    EstimatedCostNtd = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmUsages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LlmUsages_CreatedAt",
                table: "LlmUsages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LlmUsages_FeatureType_CreatedAt",
                table: "LlmUsages",
                columns: new[] { "FeatureType", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LlmRuntimeSettings");

            migrationBuilder.DropTable(
                name: "LlmUsages");
        }
    }
}
