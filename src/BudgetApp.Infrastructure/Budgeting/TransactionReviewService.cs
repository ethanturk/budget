using BudgetApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.Infrastructure.Budgeting;

public sealed record TransactionReviewFilter(int Limit, string? SearchText, decimal? MinimumAmount);

public sealed record UncategorizedTransactionReview(
    IReadOnlyList<UncategorizedTransactionSummary> Transactions,
    IReadOnlyList<TransactionCategoryOption> CategoryOptions);

public sealed record UncategorizedTransactionSummary(
    Guid TransactionId,
    DateTimeOffset PostedAt,
    string Description,
    string? Payee,
    string? Memo,
    decimal SpendingAmount);

public sealed record TransactionCategoryOption(
    Guid CategoryId,
    string GroupName,
    string CategoryName);

public sealed class TransactionReviewService
{
    private readonly BudgetAppDbContext _dbContext;

    public TransactionReviewService(BudgetAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<UncategorizedTransactionReview> GetUncategorizedTransactionsAsync(
        int limit,
        CancellationToken cancellationToken) =>
        GetUncategorizedTransactionsAsync(new TransactionReviewFilter(limit, null, null), cancellationToken);

    public async Task<UncategorizedTransactionReview> GetUncategorizedTransactionsAsync(
        TransactionReviewFilter filter,
        CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(filter.Limit, 1, 100);
        var searchText = filter.SearchText?.Trim();
        var minimumAmount = filter.MinimumAmount is > 0 ? filter.MinimumAmount.Value : (decimal?)null;

        var transactionQuery = _dbContext.Transactions
            .Where(x => x.CategoryId == null && !x.IsPending);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var normalizedSearchText = searchText.ToLower();
            transactionQuery = transactionQuery.Where(x =>
                x.Description.ToLower().Contains(normalizedSearchText)
                || (x.Payee != null && x.Payee.ToLower().Contains(normalizedSearchText))
                || (x.Memo != null && x.Memo.ToLower().Contains(normalizedSearchText)));
        }

        if (minimumAmount is not null)
        {
            transactionQuery = transactionQuery.Where(x => Math.Abs(x.Amount) >= minimumAmount.Value);
        }

        var transactions = await transactionQuery
            .OrderByDescending(x => x.PostedAt)
            .ThenBy(x => x.Description)
            .Take(safeLimit)
            .Select(x => new UncategorizedTransactionSummary(
                x.Id,
                x.PostedAt,
                x.Description,
                x.Payee,
                x.Memo,
                -x.Amount))
            .ToListAsync(cancellationToken);

        var categoryOptions = await _dbContext.Categories
            .Where(x => !x.IsArchived)
            .OrderBy(x => x.CategoryGroup.SortIndex)
            .ThenBy(x => x.SortIndex)
            .ThenBy(x => x.Name)
            .Select(x => new TransactionCategoryOption(
                x.Id,
                x.CategoryGroup.Name,
                x.Name))
            .ToListAsync(cancellationToken);

        return new UncategorizedTransactionReview(transactions, categoryOptions);
    }
}
