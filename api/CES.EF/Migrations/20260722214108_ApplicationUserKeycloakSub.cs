using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CES.EF.Migrations
{
    /// <inheritdoc />
    public partial class ApplicationUserKeycloakSub : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KeycloakSub",
                table: "ApplicationUser",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUser_KeycloakSub",
                table: "ApplicationUser",
                column: "KeycloakSub",
                unique: true,
                filter: "\"KeycloakSub\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApplicationUser_KeycloakSub",
                table: "ApplicationUser");

            migrationBuilder.DropColumn(
                name: "KeycloakSub",
                table: "ApplicationUser");
        }
    }
}
