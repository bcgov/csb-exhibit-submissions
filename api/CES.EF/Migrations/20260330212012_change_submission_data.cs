using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CES.EF.Migrations
{
    /// <inheritdoc />
    public partial class change_submission_data : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TicketNumber",
                table: "Submissions",
                newName: "AppearanceID");

            migrationBuilder.RenameColumn(
                name: "Room",
                table: "Submissions",
                newName: "RoomCode");

            migrationBuilder.RenameColumn(
                name: "Location",
                table: "Submissions",
                newName: "LocationNameText");

            migrationBuilder.RenameColumn(
                name: "DisputantName",
                table: "Submissions",
                newName: "AccusedName");

            migrationBuilder.AddColumn<string>(
                name: "LocationId",
                table: "Submissions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AccusedDOB",
                table: "Submissions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "AppearanceDateTime",
                table: "Submissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoomText",
                table: "Submissions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CourtListType",
                table: "Submissions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FileNumberText",
                table: "Submissions",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccusedDOB",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "AppearanceDateTime",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "RoomText",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "CourtListType",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "FileNumberText",
                table: "Submissions");

            migrationBuilder.RenameColumn(
                name: "AccusedName",
                table: "Submissions",
                newName: "DisputantName");

            migrationBuilder.RenameColumn(
                name: "LocationNameText",
                table: "Submissions",
                newName: "Location");

            migrationBuilder.RenameColumn(
                name: "RoomCode",
                table: "Submissions",
                newName: "Room");

            migrationBuilder.RenameColumn(
                name: "AppearanceID",
                table: "Submissions",
                newName: "TicketNumber");
        }
    }
}
