using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gasoholic.Migrations
{
    /// <inheritdoc />
    public partial class AddSignInTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastInteraction",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSignIn",
                table: "Users",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastInteraction",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastSignIn",
                table: "Users");
        }
    }
}
