using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CES.EF.Migrations
{
    /// <inheritdoc />
    public partial class MultiTicketExhibitUpload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Create the new SubmissionTickets table.
            migrationBuilder.CreateTable(
                name: "SubmissionTickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubmissionId = table.Column<int>(type: "integer", nullable: false),
                    AppearanceId = table.Column<string>(type: "text", nullable: false),
                    AppearanceDateTime = table.Column<string>(type: "text", nullable: true),
                    AppearanceSequenceNumber = table.Column<string>(type: "text", nullable: true),
                    AppearanceReasonCode = table.Column<string>(type: "text", nullable: true),
                    CourtListType = table.Column<string>(type: "text", nullable: true),
                    FileNumberText = table.Column<string>(type: "text", nullable: false),
                    AccusedName = table.Column<string>(type: "text", nullable: true),
                    AccusedDOB = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubmissionTickets_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionTickets_SubmissionId",
                table: "SubmissionTickets",
                column: "SubmissionId");

            // Step 2: Copy per-ticket data from Submissions into SubmissionTickets.
            // AppearanceSequenceNumber and AppearanceReasonCode did not exist on Submissions;
            // they are left NULL for legacy rows and populated only for new submissions.
            migrationBuilder.Sql(@"
                INSERT INTO ""SubmissionTickets"" (""SubmissionId"", ""AppearanceId"", ""AppearanceDateTime"", ""CourtListType"", ""FileNumberText"", ""AccusedName"", ""AccusedDOB"")
                SELECT ""Id"", ""AppearanceID"", ""AppearanceDateTime"", ""CourtListType"", ""FileNumberText"", ""AccusedName"", ""AccusedDOB""
                FROM ""Submissions"";
            ");

            // Step 3: Drop the now-migrated per-ticket columns from Submissions.
            migrationBuilder.DropColumn(name: "AccusedDOB",        table: "Submissions");
            migrationBuilder.DropColumn(name: "AccusedName",       table: "Submissions");
            migrationBuilder.DropColumn(name: "AppearanceDateTime", table: "Submissions");
            migrationBuilder.DropColumn(name: "AppearanceID",      table: "Submissions");
            migrationBuilder.DropColumn(name: "CourtListType",     table: "Submissions");
            migrationBuilder.DropColumn(name: "FileNumberText",    table: "Submissions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Step 1: Re-add the per-ticket columns to Submissions.
            migrationBuilder.AddColumn<string>(name: "AccusedDOB",        table: "Submissions", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "AccusedName",       table: "Submissions", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "AppearanceDateTime", table: "Submissions", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "AppearanceID",      table: "Submissions", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "CourtListType",     table: "Submissions", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "FileNumberText",    table: "Submissions", type: "text", nullable: false, defaultValue: "");

            // Step 2: Copy back the first ticket's data for each submission.
            migrationBuilder.Sql(@"
                UPDATE ""Submissions"" s
                SET ""AppearanceID""      = t.""AppearanceId"",
                    ""AppearanceDateTime"" = t.""AppearanceDateTime"",
                    ""CourtListType""     = t.""CourtListType"",
                    ""FileNumberText""    = t.""FileNumberText"",
                    ""AccusedName""       = t.""AccusedName"",
                    ""AccusedDOB""        = t.""AccusedDOB""
                FROM (
                    SELECT DISTINCT ON (""SubmissionId"") *
                    FROM ""SubmissionTickets""
                    ORDER BY ""SubmissionId"", ""Id""
                ) t
                WHERE s.""Id"" = t.""SubmissionId"";
            ");

            // Step 3: Drop SubmissionTickets.
            migrationBuilder.DropTable(name: "SubmissionTickets");
        }
    }
}
