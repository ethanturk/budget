using BudgetApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.Infrastructure.Budgeting;

public sealed record BudgetCategoryAllocationSummary(
    string CategoryName,
    decimal Amount);

public sealed record BudgetGroupSummary(
    string GroupName,
    decimal TotalBudgeted,
    IReadOnlyList<BudgetCategoryAllocationSummary> Categories);

public sealed record BudgetMonthSummary(
    DateOnly Month,
    decimal TotalBudgeted,
    int GroupCount,
    int CategoryCount,
    IReadOnlyList<BudgetGroupSummary> Groups);

public sealed class BudgetMonthSummaryService
{
    private readonly BudgetAppDbContext _dbContext;

    public BudgetMonthSummaryService(BudgetAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BudgetMonthSummary?> GetCurrentMonthSummaryAsync(CancellationToken cancellationToken)
    {
        var budgetMonth = await _dbContext.BudgetMonths
            .OrderByDescending(x => x.Month)
            .Select(x => new { x.Id, x.Month })
            .FirstOrDefaultAsync(cancellationToken);

        if (budgetMonth is null)
        {
            return null;
        }

        var allocations = await _dbContext.BudgetAllocations
            .Where(x => x.BudgetMonthId == budgetMonth.Id)
            .Select(x => new
            {
                GroupName = x.Category.CategoryGroup.Name,
                GroupSortIndex = x.Category.CategoryGroup.SortIndex,
                CategoryName = x.Category.Name,
                CategorySortIndex = x.Category.SortIndex,
                x.Amount
            })
            .ToListAsync(cancellationToken);

        var groups = allocations
            .GroupBy(x => new { x.GroupName, x.GroupSortIndex })
            .OrderBy(x => x.Key.GroupSortIndex)
            .Select(group => new BudgetGroupSummary(
                group.Key.GroupName,
                group.Sum(x => x.Amount),
                group.OrderBy(x => x.CategorySortIndex)
                    .Select(x => new BudgetCategoryAllocationSummary(x.CategoryName, x.Amount))
                    .ToList()))
            .ToList();

        return new BudgetMonthSummary(
            budgetMonth.Month,
            allocations.Sum(x => x.Amount),
            groups.Count,
            allocations.Count,
            groups);
    }
}
