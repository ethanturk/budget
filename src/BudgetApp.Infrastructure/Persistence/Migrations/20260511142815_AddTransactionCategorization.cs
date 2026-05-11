using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionCategorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_transactions_CategoryId",
                table: "transactions",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_categories_CategoryId",
                table: "transactions",
                column: "CategoryId",
                principalTable: "categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transactions_categories_CategoryId",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_CategoryId",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "transactions");
        }
    }
}
