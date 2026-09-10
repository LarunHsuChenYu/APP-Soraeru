using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Soraeru.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserLoginStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastLoginAtUtc",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoginCount",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastLoginAtUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LoginCount",
                table: "Users");
        }
    }
}
