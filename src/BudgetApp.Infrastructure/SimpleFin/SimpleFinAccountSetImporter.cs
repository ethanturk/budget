using System.Globalization;
using BudgetApp.Domain.Entities;
using BudgetApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.Infrastructure.SimpleFin;

public sealed class SimpleFinAccountSetImporter
{
    private const string Provider = "simplefin";
    private readonly BudgetAppDbContext _dbContext;

    public SimpleFinAccountSetImporter(BudgetAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SimpleFinImportSummary> ImportAsync(Guid connectionId, AccountSetResponse payload, CancellationToken cancellationToken)
    {
        var importedAt = DateTimeOffset.UtcNow;
        var accountsSeen = 0;
        var transactionsSeen = 0;
        var transactionsInserted = 0;
        var transactionsUpdated = 0;

        var institutions = await _dbContext.Institutions
            .ToDictionaryAsync(x => $"{x.Provider}:{x.ProviderInstitutionId}", cancellationToken);

        var accounts = await _dbContext.Accounts
            .Include(x => x.BalanceSnapshots)
            .Include(x => x.Transactions)
            .ToDictionaryAsync(x => $"{x.Provider}:{x.ProviderConnectionId}:{x.ProviderAccountId}", cancellationToken);

        var connectionsById = payload.Connections.ToDictionary(x => x.ConnId, StringComparer.Ordinal);

        foreach (var connection in payload.Connections)
        {
            var institutionKey = $"{Provider}:{connection.OrgId}";
            if (!institutions.TryGetValue(institutionKey, out var institution))
            {
                institution = new Institution
                {
                    Id = Guid.NewGuid(),
                    Provider = Provider,
                    ProviderInstitutionId = connection.OrgId,
                    Name = connection.Name,
                    CreatedAt = importedAt,
                    UpdatedAt = importedAt
                };

                institutions[institutionKey] = institution;
                _dbContext.Institutions.Add(institution);
            }
            else
            {
                institution.Name = connection.Name;
                institution.UpdatedAt = importedAt;
            }
        }

        foreach (var accountResponse in payload.Accounts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!connectionsById.TryGetValue(accountResponse.ConnId, out var connection))
            {
                continue;
            }

            accountsSeen++;
            var institution = institutions[$"{Provider}:{connection.OrgId}"];
            var accountKey = $"{Provider}:{accountResponse.ConnId}:{accountResponse.Id}";

            if (!accounts.TryGetValue(accountKey, out var account))
            {
                account = new Account
                {
                    Id = Guid.NewGuid(),
                    ConnectionId = connectionId,
                    Provider = Provider,
                    ProviderConnectionId = accountResponse.ConnId,
                    ProviderAccountId = accountResponse.Id,
                    CreatedAt = importedAt
                };

                accounts[accountKey] = account;
                _dbContext.Accounts.Add(account);
            }

            account.ConnectionId = connectionId;
            account.InstitutionId = institution.Id;
            account.Name = accountResponse.Name;
            account.CurrencyCode = accountResponse.Currency;
            account.UpdatedAt = importedAt;

            var balanceDate = DateTimeOffset.FromUnixTimeSeconds(accountResponse.BalanceDate);
            var existingSnapshot = account.BalanceSnapshots
                .FirstOrDefault(x => x.AsOfAt == balanceDate);

            if (existingSnapshot is null)
            {
                existingSnapshot = new AccountBalanceSnapshot
                {
                    Id = Guid.NewGuid(),
                    AccountId = account.Id,
                    AsOfAt = balanceDate
                };

                account.BalanceSnapshots.Add(existingSnapshot);
            }

            existingSnapshot.CurrencyCode = accountResponse.Currency;
            existingSnapshot.CurrentAmount = ParseDecimal(accountResponse.Balance);
            existingSnapshot.AvailableAmount = string.IsNullOrWhiteSpace(accountResponse.AvailableBalance)
                ? null
                : ParseDecimal(accountResponse.AvailableBalance);

            foreach (var transactionResponse in accountResponse.Transactions ?? [])
            {
                transactionsSeen++;

                var existingTransaction = account.Transactions.FirstOrDefault(x =>
                    x.Provider == Provider &&
                    x.ProviderConnectionId == accountResponse.ConnId &&
                    x.ProviderTransactionId == transactionResponse.Id);

                if (existingTransaction is null)
                {
                    existingTransaction = new Transaction
                    {
                        Id = Guid.NewGuid(),
                        AccountId = account.Id,
                        Provider = Provider,
                        ProviderConnectionId = accountResponse.ConnId,
                        ProviderTransactionId = transactionResponse.Id,
                        ImportedAt = importedAt
                    };

                    account.Transactions.Add(existingTransaction);
                    transactionsInserted++;
                }
                else
                {
                    transactionsUpdated++;
                }

                existingTransaction.PostedAt = DateTimeOffset.FromUnixTimeSeconds(transactionResponse.Posted);
                existingTransaction.TransactedAt = transactionResponse.TransactedAt.HasValue
                    ? DateTimeOffset.FromUnixTimeSeconds(transactionResponse.TransactedAt.Value)
                    : null;
                existingTransaction.Amount = ParseDecimal(transactionResponse.Amount);
                existingTransaction.Description = transactionResponse.Description;
                existingTransaction.IsPending = transactionResponse.Pending ?? false;
                existingTransaction.UpdatedAt = importedAt;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SimpleFinImportSummary(accountsSeen, transactionsSeen, transactionsInserted, transactionsUpdated);
    }

    private static decimal ParseDecimal(string value) =>
        decimal.Parse(value, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
}
