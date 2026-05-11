using BudgetApp.Domain.Entities;
using BudgetApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.Infrastructure.Budgeting;

public sealed record SaveBudgetAllocationRequest(
    DateOnly Month,
    string GroupName,
    string CategoryName,
    decimal Amount);

public sealed record SaveBudgetAllocationResult(
    DateOnly Month,
    string GroupName,
    string CategoryName,
    decimal Amount);

public sealed class BudgetEditorService
{
    private readonly BudgetAppDbContext _dbContext;

    public BudgetEditorService(BudgetAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SaveBudgetAllocationResult> SaveAllocationAsync(
        SaveBudgetAllocationRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedGroupName = request.GroupName.Trim();
        var normalizedCategoryName = request.CategoryName.Trim();

        if (string.IsNullOrWhiteSpace(normalizedGroupName))
        {
            throw new InvalidOperationException("Category group name is required.");
        }

        if (string.IsNullOrWhiteSpace(normalizedCategoryName))
        {
            throw new InvalidOperationException("Category name is required.");
        }

        var now = DateTimeOffset.UtcNow;

        var month = await _dbContext.BudgetMonths
            .FirstOrDefaultAsync(x => x.Month == request.Month, cancellationToken);

        if (month is null)
        {
            month = new BudgetMonth
            {
                Id = Guid.NewGuid(),
                Month = request.Month,
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.BudgetMonths.Add(month);
        }
        else
        {
            month.UpdatedAt = now;
        }

        var group = await _dbContext.CategoryGroups
            .OrderBy(x => x.SortIndex)
            .FirstOrDefaultAsync(x => x.Name == normalizedGroupName, cancellationToken);

        if (group is null)
        {
            var nextSortIndex = await _dbContext.CategoryGroups
                .Select(x => (int?)x.SortIndex)
                .MaxAsync(cancellationToken) ?? 0;

            group = new CategoryGroup
            {
                Id = Guid.NewGuid(),
                Name = normalizedGroupName,
                SortIndex = nextSortIndex + 1,
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.CategoryGroups.Add(group);
        }
        else
        {
            group.UpdatedAt = now;
        }

        var category = await _dbContext.Categories
            .OrderBy(x => x.SortIndex)
            .FirstOrDefaultAsync(
                x => x.CategoryGroupId == group.Id && x.Name == normalizedCategoryName,
                cancellationToken);

        if (category is null)
        {
            var nextSortIndex = await _dbContext.Categories
                .Where(x => x.CategoryGroupId == group.Id)
                .Select(x => (int?)x.SortIndex)
                .MaxAsync(cancellationToken) ?? 0;

            category = new Category
            {
                Id = Guid.NewGuid(),
                CategoryGroupId = group.Id,
                Name = normalizedCategoryName,
                SortIndex = nextSortIndex + 1,
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.Categories.Add(category);
        }
        else
        {
            category.UpdatedAt = now;
        }

        var allocation = await _dbContext.BudgetAllocations
            .FirstOrDefaultAsync(
                x => x.BudgetMonthId == month.Id && x.CategoryId == category.Id,
                cancellationToken);

        if (allocation is null)
        {
            allocation = new BudgetAllocation
            {
                Id = Guid.NewGuid(),
                BudgetMonthId = month.Id,
                CategoryId = category.Id,
                Amount = request.Amount,
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.BudgetAllocations.Add(allocation);
        }
        else
        {
            allocation.Amount = request.Amount;
            allocation.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SaveBudgetAllocationResult(
            month.Month,
            group.Name,
            category.Name,
            allocation.Amount);
    }
}
