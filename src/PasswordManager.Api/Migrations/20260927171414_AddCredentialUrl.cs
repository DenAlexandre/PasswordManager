using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PasswordManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCredentialUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EncryptedUrl",
                table: "Credentials",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EncryptedUrl",
                table: "Credentials");
        }
    }
}
