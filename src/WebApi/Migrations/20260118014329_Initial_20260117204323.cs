using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApi.Migrations
{
    /// <inheritdoc />
    public partial class Initial_20260117204323 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MessageAgentActions",
                schema: "silver_surfers_main",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Reasoning = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    PageUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PageHtml = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageAgentActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageAgentActions_TaskSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "silver_surfers_main",
                        principalTable: "TaskSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageAgentActions_CreatedAt",
                schema: "silver_surfers_main",
                table: "MessageAgentActions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MessageAgentActions_SessionId",
                schema: "silver_surfers_main",
                table: "MessageAgentActions",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessageAgentActions",
                schema: "silver_surfers_main");
        }
    }
}
