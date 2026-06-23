using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CES.EF.Migrations
{
    /// <inheritdoc />
    public partial class AdminListingUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StatusChangedDateUTC",
                table: "Submissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUTC",
                table: "StoredFiles",
                type: "timestamp with time zone",
                nullable: true);

            // Dev-only backfill: classify existing IsDeleted submissions as Accepted
            // and clear IsDeleted so they remain visible in the historical listing.
            migrationBuilder.Sql(@"
                UPDATE ""Submissions""
                SET ""Status"" = 1, ""IsDeleted"" = false
                WHERE ""IsDeleted"" = true;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "StatusChangedDateUTC",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "DeletedAtUTC",
                table: "StoredFiles");
        }
    }
}
