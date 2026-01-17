using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApi.Migrations
{
    /// <inheritdoc />
    public partial class Initial_20260117182846 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentActions",
                schema: "silver_surfers_main");

            migrationBuilder.CreateTable(
                name: "ClickAgentActions",
                schema: "silver_surfers_main",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Target = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Reasoning = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PageUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PageHtml = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClickAgentActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClickAgentActions_TaskSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "silver_surfers_main",
                        principalTable: "TaskSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompleteAgentActions",
                schema: "silver_surfers_main",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Reasoning = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PageUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PageHtml = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompleteAgentActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompleteAgentActions_TaskSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "silver_surfers_main",
                        principalTable: "TaskSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WaitAgentActions",
                schema: "silver_surfers_main",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Duration = table.Column<int>(type: "integer", nullable: false),
                    Reasoning = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PageUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PageHtml = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitAgentActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaitAgentActions_TaskSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "silver_surfers_main",
                        principalTable: "TaskSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClickAgentActions_CreatedAt",
                schema: "silver_surfers_main",
                table: "ClickAgentActions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ClickAgentActions_SessionId",
                schema: "silver_surfers_main",
                table: "ClickAgentActions",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompleteAgentActions_CreatedAt",
                schema: "silver_surfers_main",
                table: "CompleteAgentActions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CompleteAgentActions_SessionId",
                schema: "silver_surfers_main",
                table: "CompleteAgentActions",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_WaitAgentActions_CreatedAt",
                schema: "silver_surfers_main",
                table: "WaitAgentActions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WaitAgentActions_SessionId",
                schema: "silver_surfers_main",
                table: "WaitAgentActions",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClickAgentActions",
                schema: "silver_surfers_main");

            migrationBuilder.DropTable(
                name: "CompleteAgentActions",
                schema: "silver_surfers_main");

            migrationBuilder.DropTable(
                name: "WaitAgentActions",
                schema: "silver_surfers_main");

            migrationBuilder.CreateTable(
                name: "AgentActions",
                schema: "silver_surfers_main",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PageHtml = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: true),
                    PageUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Reasoning = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    Target = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
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
        }
    }
}
