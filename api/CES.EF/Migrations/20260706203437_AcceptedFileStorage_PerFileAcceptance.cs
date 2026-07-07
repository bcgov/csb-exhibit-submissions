using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CES.EF.Migrations
{
    /// <inheritdoc />
    public partial class AcceptedFileStorage_PerFileAcceptance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedAtUTC",
                table: "StoredFiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcceptedFileName",
                table: "StoredFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CanonicalPath",
                table: "StoredFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAccepted",
                table: "StoredFiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Sha256",
                table: "StoredFiles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptedAtUTC",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "AcceptedFileName",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "CanonicalPath",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "IsAccepted",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "Sha256",
                table: "StoredFiles");
        }
    }
}
