using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CES.EF.Migrations
{
    /// <inheritdoc />
    public partial class ExhibitClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StoredFiles_Submissions_SubmissionId",
                table: "StoredFiles");

            migrationBuilder.AlterColumn<int>(
                name: "SubmissionId",
                table: "StoredFiles",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "StoredFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EnteredAt",
                table: "StoredFiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnteredValue",
                table: "StoredFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MarkedAt",
                table: "StoredFiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MarkedValue",
                table: "StoredFiles",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SubmissionAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubmissionId = table.Column<int>(type: "integer", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: true),
                    FieldName = table.Column<string>(type: "text", nullable: false),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    ChangedBy = table.Column<string>(type: "text", nullable: true),
                    ChangedAtUTC = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubmissionAuditLogs_StoredFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SubmissionAuditLogs_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionAuditLogs_FileId",
                table: "SubmissionAuditLogs",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionAuditLogs_SubmissionId",
                table: "SubmissionAuditLogs",
                column: "SubmissionId");

            migrationBuilder.AddForeignKey(
                name: "FK_StoredFiles_Submissions_SubmissionId",
                table: "StoredFiles",
                column: "SubmissionId",
                principalTable: "Submissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StoredFiles_Submissions_SubmissionId",
                table: "StoredFiles");

            migrationBuilder.DropTable(
                name: "SubmissionAuditLogs");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "EnteredAt",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "EnteredValue",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "MarkedAt",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "MarkedValue",
                table: "StoredFiles");

            migrationBuilder.AlterColumn<int>(
                name: "SubmissionId",
                table: "StoredFiles",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_StoredFiles_Submissions_SubmissionId",
                table: "StoredFiles",
                column: "SubmissionId",
                principalTable: "Submissions",
                principalColumn: "Id");
        }
    }
}
