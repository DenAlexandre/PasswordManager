using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PasswordManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteNesting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentSiteId",
                table: "Sites",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sites_ParentSiteId",
                table: "Sites",
                column: "ParentSiteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sites_Sites_ParentSiteId",
                table: "Sites",
                column: "ParentSiteId",
                principalTable: "Sites",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sites_Sites_ParentSiteId",
                table: "Sites");

            migrationBuilder.DropIndex(
                name: "IX_Sites_ParentSiteId",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "ParentSiteId",
                table: "Sites");
        }
    }
}
