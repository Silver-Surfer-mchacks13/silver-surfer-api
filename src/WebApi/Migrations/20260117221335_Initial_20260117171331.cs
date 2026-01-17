using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApi.Migrations
{
    /// <inheritdoc />
    public partial class Initial_20260117171331 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SitePatterns",
                schema: "silver_surfers_main",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Domain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PatternJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SitePatterns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskSessions",
                schema: "silver_surfers_main",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Goal = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskSessions_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "silver_surfers_main",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AgentActions",
                schema: "silver_surfers_main",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Target = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Reasoning = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PageUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PageHtml = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentActions_TaskSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "silver_surfers_main",
                        principalTable: "TaskSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentActions_ActionType",
                schema: "silver_surfers_main",
                table: "AgentActions",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "IX_AgentActions_CreatedAt",
                schema: "silver_surfers_main",
                table: "AgentActions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AgentActions_SessionId",
                schema: "silver_surfers_main",
                table: "AgentActions",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SitePatterns_Domain",
                schema: "silver_surfers_main",
                table: "SitePatterns",
                column: "Domain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskSessions_CreatedAt",
                schema: "silver_surfers_main",
                table: "TaskSessions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TaskSessions_Status",
                schema: "silver_surfers_main",
                table: "TaskSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TaskSessions_UserId",
                schema: "silver_surfers_main",
                table: "TaskSessions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentActions",
                schema: "silver_surfers_main");

            migrationBuilder.DropTable(
                name: "SitePatterns",
                schema: "silver_surfers_main");

            migrationBuilder.DropTable(
                name: "TaskSessions",
                schema: "silver_surfers_main");
        }
    }
}
