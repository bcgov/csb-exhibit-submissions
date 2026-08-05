using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CES.EF.Migrations
{
    /// <summary>
    /// Drops <c>ApplicationUser.Password</c> (Keycloak owns authentication) and replaces every
    /// free-text audit column with a nullable FK to <c>ApplicationUser.Id</c>.
    /// <para>
    /// DATA LOSS, intentional: the old columns held role labels ("Admin", "Officer") and
    /// display strings that cannot be mapped back to a user row, so they are dropped rather
    /// than migrated. Rows predating this migration therefore have no attributed actor.
    /// </para>
    /// </summary>
    public partial class AuditUserLinkage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "UserAuthTokens");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "UserAuthTokens");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ChangedBy",
                table: "SubmissionAuditLogs");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ExhibitNotes");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ExhibitDescriptions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ApplicationUser");

            migrationBuilder.DropColumn(
                name: "Password",
                table: "ApplicationUser");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "ApplicationUser");

            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "UserAuthTokens",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "UserAuthTokens",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "Submissions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "Submissions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChangedByUserId",
                table: "SubmissionAuditLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "StoredFiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "StoredFiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "ExhibitNotes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "ExhibitDescriptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "ApplicationUser",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "ApplicationUser",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAuthTokens_CreatedByUserId",
                table: "UserAuthTokens",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAuthTokens_UpdatedByUserId",
                table: "UserAuthTokens",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_CreatedByUserId",
                table: "Submissions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_UpdatedByUserId",
                table: "Submissions",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionAuditLogs_ChangedByUserId",
                table: "SubmissionAuditLogs",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_CreatedByUserId",
                table: "StoredFiles",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_UpdatedByUserId",
                table: "StoredFiles",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExhibitNotes_CreatedByUserId",
                table: "ExhibitNotes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExhibitDescriptions_CreatedByUserId",
                table: "ExhibitDescriptions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUser_CreatedByUserId",
                table: "ApplicationUser",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUser_UpdatedByUserId",
                table: "ApplicationUser",
                column: "UpdatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicationUser_ApplicationUser_CreatedByUserId",
                table: "ApplicationUser",
                column: "CreatedByUserId",
                principalTable: "ApplicationUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicationUser_ApplicationUser_UpdatedByUserId",
                table: "ApplicationUser",
                column: "UpdatedByUserId",
                principalTable: "ApplicationUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExhibitDescriptions_ApplicationUser_CreatedByUserId",
                table: "ExhibitDescriptions",
                column: "CreatedByUserId",
                principalTable: "ApplicationUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExhibitNotes_ApplicationUser_CreatedByUserId",
                table: "ExhibitNotes",
                column: "CreatedByUserId",
                principalTable: "ApplicationUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StoredFiles_ApplicationUser_CreatedByUserId",
                table: "StoredFiles",
                column: "CreatedByUserId",
                principalTable: "ApplicationUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StoredFiles_ApplicationUser_UpdatedByUserId",
                table: "StoredFiles",
                column: "UpdatedByUserId",
                principalTable: "ApplicationUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubmissionAuditLogs_ApplicationUser_ChangedByUserId",
                table: "SubmissionAuditLogs",
                column: "ChangedByUserId",
                principalTable: "ApplicationUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_ApplicationUser_CreatedByUserId",
                table: "Submissions",
                column: "CreatedByUserId",
                principalTable: "ApplicationUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_ApplicationUser_UpdatedByUserId",
                table: "Submissions",
                column: "UpdatedByUserId",
                principalTable: "ApplicationUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserAuthTokens_ApplicationUser_CreatedByUserId",
                table: "UserAuthTokens",
                column: "CreatedByUserId",
                principalTable: "ApplicationUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserAuthTokens_ApplicationUser_UpdatedByUserId",
                table: "UserAuthTokens",
                column: "UpdatedByUserId",
                principalTable: "ApplicationUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApplicationUser_ApplicationUser_CreatedByUserId",
                table: "ApplicationUser");

            migrationBuilder.DropForeignKey(
                name: "FK_ApplicationUser_ApplicationUser_UpdatedByUserId",
                table: "ApplicationUser");

            migrationBuilder.DropForeignKey(
                name: "FK_ExhibitDescriptions_ApplicationUser_CreatedByUserId",
                table: "ExhibitDescriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_ExhibitNotes_ApplicationUser_CreatedByUserId",
                table: "ExhibitNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_StoredFiles_ApplicationUser_CreatedByUserId",
                table: "StoredFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_StoredFiles_ApplicationUser_UpdatedByUserId",
                table: "StoredFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_SubmissionAuditLogs_ApplicationUser_ChangedByUserId",
                table: "SubmissionAuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_ApplicationUser_CreatedByUserId",
                table: "Submissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_ApplicationUser_UpdatedByUserId",
                table: "Submissions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserAuthTokens_ApplicationUser_CreatedByUserId",
                table: "UserAuthTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_UserAuthTokens_ApplicationUser_UpdatedByUserId",
                table: "UserAuthTokens");

            migrationBuilder.DropIndex(
                name: "IX_UserAuthTokens_CreatedByUserId",
                table: "UserAuthTokens");

            migrationBuilder.DropIndex(
                name: "IX_UserAuthTokens_UpdatedByUserId",
                table: "UserAuthTokens");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_CreatedByUserId",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_UpdatedByUserId",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_SubmissionAuditLogs_ChangedByUserId",
                table: "SubmissionAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_StoredFiles_CreatedByUserId",
                table: "StoredFiles");

            migrationBuilder.DropIndex(
                name: "IX_StoredFiles_UpdatedByUserId",
                table: "StoredFiles");

            migrationBuilder.DropIndex(
                name: "IX_ExhibitNotes_CreatedByUserId",
                table: "ExhibitNotes");

            migrationBuilder.DropIndex(
                name: "IX_ExhibitDescriptions_CreatedByUserId",
                table: "ExhibitDescriptions");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationUser_CreatedByUserId",
                table: "ApplicationUser");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationUser_UpdatedByUserId",
                table: "ApplicationUser");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "UserAuthTokens");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "UserAuthTokens");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ChangedByUserId",
                table: "SubmissionAuditLogs");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "ExhibitNotes");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "ExhibitDescriptions");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "ApplicationUser");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "ApplicationUser");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "UserAuthTokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "UserAuthTokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Submissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Submissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangedBy",
                table: "SubmissionAuditLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "StoredFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "StoredFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ExhibitNotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ExhibitDescriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ApplicationUser",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "ApplicationUser",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "ApplicationUser",
                type: "text",
                nullable: true);
        }
    }
}
