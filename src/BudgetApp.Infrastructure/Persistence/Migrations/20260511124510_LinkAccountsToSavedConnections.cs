using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkAccountsToSavedConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConnectionId",
                table: "accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_ConnectionId",
                table: "accounts",
                column: "ConnectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_accounts_simplefin_connections_ConnectionId",
                table: "accounts",
                column: "ConnectionId",
                principalTable: "simplefin_connections",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_accounts_simplefin_connections_ConnectionId",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "IX_accounts_ConnectionId",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "ConnectionId",
                table: "accounts");
        }
    }
}
