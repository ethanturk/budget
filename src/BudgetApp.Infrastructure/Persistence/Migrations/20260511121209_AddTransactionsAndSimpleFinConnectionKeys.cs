using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionsAndSimpleFinConnectionKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_accounts_Provider_ProviderAccountId",
                table: "accounts");

            migrationBuilder.AddColumn<string>(
                name: "ProviderConnectionId",
                table: "accounts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    ProviderConnectionId = table.Column<string>(type: "text", nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "text", nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TransactedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IsPending = table.Column<bool>(type: "boolean", nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_transactions_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_Provider_ProviderConnectionId_ProviderAccountId",
                table: "accounts",
                columns: new[] { "Provider", "ProviderConnectionId", "ProviderAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transactions_AccountId_PostedAt",
                table: "transactions",
                columns: new[] { "AccountId", "PostedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_transactions_AccountId_Provider_ProviderConnectionId_Provid~",
                table: "transactions",
                columns: new[] { "AccountId", "Provider", "ProviderConnectionId", "ProviderTransactionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_accounts_Provider_ProviderConnectionId_ProviderAccountId",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "ProviderConnectionId",
                table: "accounts");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_Provider_ProviderAccountId",
                table: "accounts",
                columns: new[] { "Provider", "ProviderAccountId" },
                unique: true);
        }
    }
}
