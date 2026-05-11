using BudgetApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.Infrastructure.Budgeting;

public sealed record CategorizeTransactionResult(
    Guid TransactionId,
    Guid CategoryId,
    string CategoryName);

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

        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(x => x.Id == categoryId, cancellationToken);

        if (category is null)
        {
            throw new InvalidOperationException("Category was not found.");
        }

        transaction.CategoryId = category.Id;
        transaction.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CategorizeTransactionResult(transaction.Id, category.Id, category.Name);
    }
}
