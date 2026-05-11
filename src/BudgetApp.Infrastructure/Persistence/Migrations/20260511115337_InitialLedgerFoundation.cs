using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialLedgerFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "institutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    ProviderInstitutionId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "simplefin_connections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    AccessUrlCiphertext = table.Column<string>(type: "text", nullable: false),
                    AccessUrlHint = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    LastSuccessfulSyncAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAttemptedSyncAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_simplefin_connections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    ProviderAccountId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    OfficialName = table.Column<string>(type: "text", nullable: true),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    AccountType = table.Column<string>(type: "text", nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    Last4 = table.Column<string>(type: "text", nullable: true),
                    SortIndex = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_accounts_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "sync_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggerSource = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AccountsSeen = table.Column<int>(type: "integer", nullable: false),
                    TransactionsSeen = table.Column<int>(type: "integer", nullable: false),
                    TransactionsInserted = table.Column<int>(type: "integer", nullable: false),
                    TransactionsUpdated = table.Column<int>(type: "integer", nullable: false),
                    ErrorText = table.Column<string>(type: "text", nullable: true),
                    LockOwner = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sync_runs_simplefin_connections_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "simplefin_connections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "account_balance_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AsOfAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AvailableAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CurrentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LimitAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    SyncRunId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_balance_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_account_balance_snapshots_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_account_balance_snapshots_sync_runs_SyncRunId",
                        column: x => x.SyncRunId,
                        principalTable: "sync_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_balance_snapshots_AccountId_AsOfAt",
                table: "account_balance_snapshots",
                columns: new[] { "AccountId", "AsOfAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_balance_snapshots_SyncRunId",
                table: "account_balance_snapshots",
                column: "SyncRunId");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_InstitutionId",
                table: "accounts",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_Provider_ProviderAccountId",
                table: "accounts",
                columns: new[] { "Provider", "ProviderAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_institutions_Provider_ProviderInstitutionId",
                table: "institutions",
                columns: new[] { "Provider", "ProviderInstitutionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sync_runs_ConnectionId_StartedAt",
                table: "sync_runs",
                columns: new[] { "ConnectionId", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_balance_snapshots");

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "sync_runs");

            migrationBuilder.DropTable(
                name: "institutions");

            migrationBuilder.DropTable(
                name: "simplefin_connections");
        }
    }
}
