using BudgetApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.Infrastructure.Reporting;

public sealed record AccountTransactionDetail(
    Guid AccountId,
    string AccountName,
    string? InstitutionName,
    decimal CurrentBalance,
    decimal? AvailableBalance,
    string CurrencyCode,
    bool IsClosed,
    IReadOnlyList<AccountTransactionListItem> Transactions);

public sealed record AccountTransactionListItem(
    DateTimeOffset PostedAt,
    string Description,
    decimal Amount,
    bool IsPending,
    string? CategoryName);

public sealed class AccountTransactionQueryService
{
    private readonly BudgetAppDbContext _dbContext;

    public AccountTransactionQueryService(BudgetAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AccountTransactionDetail?> GetAccountTransactionsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await _dbContext.Accounts
            .Where(x => x.Id == accountId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                InstitutionName = x.Institution != null ? x.Institution.Name : null,
                x.CurrencyCode,
                x.IsClosed,
                CurrentBalance = x.BalanceSnapshots
                    .OrderByDescending(snapshot => snapshot.AsOfAt)
                    .Select(snapshot => (decimal?)snapshot.CurrentAmount)
                    .FirstOrDefault() ?? 0m,
                AvailableBalance = x.BalanceSnapshots
                    .OrderByDescending(snapshot => snapshot.AsOfAt)
                    .Select(snapshot => snapshot.AvailableAmount)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (account is null)
        {
            return null;
        }

        var transactions = await _dbContext.Transactions
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.PostedAt)
            .ThenByDescending(x => x.ImportedAt)
            .Select(x => new AccountTransactionListItem(
                x.PostedAt,
                x.Description,
                x.Amount,
                x.IsPending,
                x.Category != null ? x.Category.Name : null))
            .ToListAsync(cancellationToken);

        return new AccountTransactionDetail(
            account.Id,
            account.Name,
            account.InstitutionName,
            account.CurrentBalance,
            account.AvailableBalance,
            account.CurrencyCode,
            account.IsClosed,
            transactions);
    }
}
