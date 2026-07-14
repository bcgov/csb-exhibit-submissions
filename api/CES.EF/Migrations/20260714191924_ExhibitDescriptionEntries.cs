using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CES.EF.Migrations
{
    /// <inheritdoc />
    public partial class ExhibitDescriptionEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "StoredFiles");

            migrationBuilder.CreateTable(
                name: "ExhibitDescriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DescriptionText = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUTC = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExhibitDescriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExhibitDescriptions_StoredFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExhibitDescriptions_FileId_CreatedAtUTC",
                table: "ExhibitDescriptions",
                columns: new[] { "FileId", "CreatedAtUTC" });

            // Descriptions are no longer an audited field — the append-only entry list
            // is their history. Drop the legacy rows so the change-history UI does not
            // report a field that no longer exists (CES-42).
            migrationBuilder.Sql("DELETE FROM \"SubmissionAuditLogs\" WHERE \"FieldName\" = 'Description';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExhibitDescriptions");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "StoredFiles",
                type: "text",
                nullable: true);
        }
    }
}
