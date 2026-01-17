using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApi.Migrations
{
    /// <inheritdoc />
    public partial class Initial_20260117173909 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskSessions_Status",
                schema: "silver_surfers_main",
                table: "TaskSessions");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "silver_surfers_main",
                table: "TaskSessions");

            migrationBuilder.RenameColumn(
                name: "Goal",
                schema: "silver_surfers_main",
                table: "TaskSessions",
                newName: "Title");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Title",
                schema: "silver_surfers_main",
                table: "TaskSessions",
                newName: "Goal");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "silver_surfers_main",
                table: "TaskSessions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_TaskSessions_Status",
                schema: "silver_surfers_main",
                table: "TaskSessions",
                column: "Status");
        }
    }
}
