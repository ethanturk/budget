using BudgetApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.Infrastructure.Budgeting;

public sealed record UncategorizedTransactionReview(
    IReadOnlyList<UncategorizedTransactionSummary> Transactions,
    IReadOnlyList<TransactionCategoryOption> CategoryOptions);

public sealed record UncategorizedTransactionSummary(
    Guid TransactionId,
    DateTimeOffset PostedAt,
    string Description,
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

    public async Task<UncategorizedTransactionReview> GetUncategorizedTransactionsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);

        var transactions = await _dbContext.Transactions
            .Where(x => x.CategoryId == null && !x.IsPending)
            .OrderByDescending(x => x.PostedAt)
            .ThenBy(x => x.Description)
            .Take(safeLimit)
            .Select(x => new UncategorizedTransactionSummary(
                x.Id,
                x.PostedAt,
                x.Description,
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
