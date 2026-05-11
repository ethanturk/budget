using BudgetApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.Infrastructure.Budgeting;

public enum SpendingStatus
{
    OnTrack,
    NearLimit,
    OverBudget
}

public sealed record MonthlySpendingReport(
    DateOnly Month,
    decimal TotalBudgeted,
    decimal TotalSpent,
    decimal TotalRemaining,
    decimal UncategorizedSpent,
    IReadOnlyList<MonthlySpendingGroup> Groups);

public sealed record MonthlySpendingGroup(
    string GroupName,
    decimal Budgeted,
    decimal Spent,
    decimal Remaining,
    IReadOnlyList<MonthlySpendingCategory> Categories);

public sealed record MonthlySpendingCategory(
    Guid CategoryId,
    string CategoryName,
    decimal Budgeted,
    decimal Spent,
    decimal Remaining,
    decimal PercentSpent,
    SpendingStatus Status);

public sealed class SpendingReportService
{
    private readonly BudgetAppDbContext _dbContext;

    public SpendingReportService(BudgetAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MonthlySpendingReport> GetMonthlyReportAsync(
        DateOnly month,
        CancellationToken cancellationToken)
    {
        var start = new DateTimeOffset(month.Year, month.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddMonths(1);

        var budgetMonth = await _dbContext.BudgetMonths
            .Where(x => x.Month == month)
            .Select(x => new { x.Id, x.Month })
            .FirstOrDefaultAsync(cancellationToken);

        var budgetRows = budgetMonth is null
            ? []
            : await _dbContext.BudgetAllocations
                .Where(x => x.BudgetMonthId == budgetMonth.Id)
                .Select(x => new SpendingRow(
                    x.CategoryId,
                    x.Category.CategoryGroup.Name,
                    x.Category.CategoryGroup.SortIndex,
                    x.Category.Name,
                    x.Category.SortIndex,
                    x.Amount))
                .ToListAsync(cancellationToken);

        var spendingRows = await _dbContext.Transactions
            .Where(x => x.PostedAt >= start && x.PostedAt < end && !x.IsPending)
            .Select(x => new
            {
                x.CategoryId,
                SpendingAmount = -x.Amount,
                GroupName = x.Category == null ? null : x.Category.CategoryGroup.Name,
                GroupSortIndex = x.Category == null ? 0 : x.Category.CategoryGroup.SortIndex,
                CategoryName = x.Category == null ? null : x.Category.Name,
                CategorySortIndex = x.Category == null ? 0 : x.Category.SortIndex
            })
            .ToListAsync(cancellationToken);

        var categorizedSpending = spendingRows
            .Where(x => x.CategoryId.HasValue)
            .GroupBy(x => x.CategoryId!.Value)
            .ToDictionary(x => x.Key, x => x.Sum(row => row.SpendingAmount));

        var categories = budgetRows
            .Select(x => new
            {
                x.CategoryId,
                x.GroupName,
                x.GroupSortIndex,
                x.CategoryName,
                x.CategorySortIndex,
                Budgeted = x.Budgeted,
                Spent = categorizedSpending.GetValueOrDefault(x.CategoryId)
            })
            .Concat(spendingRows
                .Where(x => x.CategoryId.HasValue && !budgetRows.Any(budget => budget.CategoryId == x.CategoryId.Value))
                .GroupBy(x => new
                {
                    CategoryId = x.CategoryId!.Value,
                    GroupName = x.GroupName ?? "Uncategorized",
                    x.GroupSortIndex,
                    CategoryName = x.CategoryName ?? "Uncategorized",
                    x.CategorySortIndex
                })
                .Select(x => new
                {
                    x.Key.CategoryId,
                    x.Key.GroupName,
                    x.Key.GroupSortIndex,
                    x.Key.CategoryName,
                    x.Key.CategorySortIndex,
                    Budgeted = 0m,
                    Spent = x.Sum(row => row.SpendingAmount)
                }))
            .ToList();

        var groups = categories
            .GroupBy(x => new { x.GroupName, x.GroupSortIndex })
            .OrderBy(x => x.Key.GroupSortIndex)
            .ThenBy(x => x.Key.GroupName)
            .Select(group =>
            {
                var groupCategories = group
                    .OrderBy(x => x.CategorySortIndex)
                    .ThenBy(x => x.CategoryName)
                    .Select(x =>
                    {
                        var remaining = x.Budgeted - x.Spent;
                        var percentSpent = CalculatePercentSpent(x.Budgeted, x.Spent);

                        return new MonthlySpendingCategory(
                            x.CategoryId,
                            x.CategoryName,
                            x.Budgeted,
                            x.Spent,
                            remaining,
                            percentSpent,
                            DetermineStatus(percentSpent));
                    })
                    .ToList();
                var budgeted = groupCategories.Sum(x => x.Budgeted);
                var spent = groupCategories.Sum(x => x.Spent);

                return new MonthlySpendingGroup(
                    group.Key.GroupName,
                    budgeted,
                    spent,
                    budgeted - spent,
                    groupCategories);
            })
            .ToList();

        var totalBudgeted = groups.Sum(x => x.Budgeted);
        var totalSpent = groups.Sum(x => x.Spent);
        var uncategorizedSpent = spendingRows
            .Where(x => !x.CategoryId.HasValue)
            .Sum(x => x.SpendingAmount);

        return new MonthlySpendingReport(
            month,
            totalBudgeted,
            totalSpent,
            totalBudgeted - totalSpent,
            uncategorizedSpent,
            groups);
    }

    private static decimal CalculatePercentSpent(decimal budgeted, decimal spent)
    {
        if (budgeted <= 0)
        {
            return spent > 0 ? 100m : 0m;
        }

        return Math.Round(spent / budgeted * 100m, 2, MidpointRounding.AwayFromZero);
    }

    private static SpendingStatus DetermineStatus(decimal percentSpent)
    {
        if (percentSpent > 100m)
        {
            return SpendingStatus.OverBudget;
        }

        return percentSpent >= 90m ? SpendingStatus.NearLimit : SpendingStatus.OnTrack;
    }

    private sealed record SpendingRow(
        Guid CategoryId,
        string GroupName,
        int GroupSortIndex,
        string CategoryName,
        int CategorySortIndex,
        decimal Budgeted);
}
