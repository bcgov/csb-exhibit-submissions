using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CES.EF.Migrations
{
    /// <inheritdoc />
    public partial class EvidenceSourceType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EvidenceSourceType",
                table: "StoredFiles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EvidenceSourceType",
                table: "StoredFiles");
        }
    }
}
