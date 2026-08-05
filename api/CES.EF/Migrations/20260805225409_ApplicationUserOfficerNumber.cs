using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CES.EF.Migrations
{
    /// <inheritdoc />
    public partial class ApplicationUserOfficerNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OfficerNumber",
                table: "ApplicationUser",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OfficerNumber",
                table: "ApplicationUser");
        }
    }
}
