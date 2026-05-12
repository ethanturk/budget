using BudgetApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.Infrastructure.Budgeting;

public sealed record CategorizeTransactionResult(
    Guid TransactionId,
    Guid CategoryId,
    string CategoryName);

public sealed record BulkCategorizeTransactionsRequest(
    Guid CategoryId,
    TransactionReviewFilter Filter);

public sealed record BulkCategorizeTransactionsResult(
    Guid CategoryId,
    string CategoryName,
    int CategorizedCount,
    IReadOnlyList<Guid> TransactionIds);

public sealed class TransactionCategorizationService
{
    private readonly BudgetAppDbContext _dbContext;

    public TransactionCategorizationService(BudgetAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CategorizeTransactionResult> CategorizeTransactionAsync(
        Guid transactionId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var transaction = await _dbContext.Transactions
            .FirstOrDefaultAsync(x => x.Id == transactionId, cancellationToken);

        if (transaction is null)
        {
            throw new InvalidOperationException("Transaction was not found.");
        }

        var category = await GetCategoryAsync(categoryId, cancellationToken);

        transaction.CategoryId = category.Id;
        transaction.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CategorizeTransactionResult(transaction.Id, category.Id, category.Name);
    }

    public async Task<BulkCategorizeTransactionsResult> BulkCategorizeTransactionsAsync(
        BulkCategorizeTransactionsRequest request,
        CancellationToken cancellationToken)
    {
        var category = await GetCategoryAsync(request.CategoryId, cancellationToken);
        var safeLimit = Math.Clamp(request.Filter.Limit, 1, 100);
        var searchText = request.Filter.SearchText?.Trim();
        var minimumAmount = request.Filter.MinimumAmount is > 0 ? request.Filter.MinimumAmount.Value : (decimal?)null;

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
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var transaction in transactions)
        {
            transaction.CategoryId = category.Id;
            transaction.UpdatedAt = now;
        }

        if (transactions.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new BulkCategorizeTransactionsResult(
            category.Id,
            category.Name,
            transactions.Count,
            transactions.Select(x => x.Id).ToList());
    }

    private async Task<Domain.Entities.Category> GetCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(x => x.Id == categoryId, cancellationToken);

        if (category is null)
        {
            throw new InvalidOperationException("Category was not found.");
        }

        return category;
    }
}
