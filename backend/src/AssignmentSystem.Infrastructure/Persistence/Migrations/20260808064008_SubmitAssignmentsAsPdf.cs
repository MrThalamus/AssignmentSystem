using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssignmentSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubmitAssignmentsAsPdf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A submission is now a PDF, and a typed answer cannot be turned into one.
            // Dropping "Content" below already destroys what those rows held, so the rows
            // go too rather than surviving as submissions with no file behind them -
            // unopenable for the teacher and unfixable for the student, who cannot
            // replace work the assignment has since stopped accepting. Only the old demo
            // submissions are affected; the seeder no longer creates any.
            migrationBuilder.Sql("DELETE FROM submissions;");

            migrationBuilder.DropColumn(
                name: "AttachmentUrl",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "Content",
                table: "submissions");

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "submissions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "submissions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "FileSizeBytes",
                table: "submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "submission_files",
                columns: table => new
                {
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_files", x => x.SubmissionId);
                    table.ForeignKey(
                        name: "FK_submission_files_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "submission_files");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "submissions");

            migrationBuilder.AddColumn<string>(
                name: "AttachmentUrl",
                table: "submissions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Content",
                table: "submissions",
                type: "character varying(20000)",
                maxLength: 20000,
                nullable: false,
                defaultValue: "");
        }
    }
}
