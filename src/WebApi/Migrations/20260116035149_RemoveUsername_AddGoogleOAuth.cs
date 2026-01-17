using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApi.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUsername_AddGoogleOAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                schema: "asp_template",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                schema: "asp_template",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Username",
                schema: "asp_template",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                schema: "asp_template",
                table: "Users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "asp_template",
                table: "Users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                schema: "asp_template",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Local");

            migrationBuilder.AddColumn<string>(
                name: "ProviderUserId",
                schema: "asp_template",
                table: "Users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
            
            // Update existing users to have Provider = "Local"
            migrationBuilder.Sql(
                @"UPDATE asp_template.""Users"" SET ""Provider"" = 'Local' WHERE ""Provider"" = '';");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Provider_Email",
                schema: "asp_template",
                table: "Users",
                columns: new[] { "Provider", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Provider_ProviderUserId",
                schema: "asp_template",
                table: "Users",
                columns: new[] { "Provider", "ProviderUserId" },
                unique: true,
                filter: "\"ProviderUserId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Provider_Email",
                schema: "asp_template",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Provider_ProviderUserId",
                schema: "asp_template",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Provider",
                schema: "asp_template",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProviderUserId",
                schema: "asp_template",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                schema: "asp_template",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "asp_template",
                table: "Users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AddColumn<string>(
                name: "Username",
                schema: "asp_template",
                table: "Users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                schema: "asp_template",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "\"Email\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                schema: "asp_template",
                table: "Users",
                column: "Username",
                unique: true);
        }
    }
}
