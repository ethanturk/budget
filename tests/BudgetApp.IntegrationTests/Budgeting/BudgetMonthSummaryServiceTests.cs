using BudgetApp.Domain.Entities;
using BudgetApp.Infrastructure.Budgeting;
using BudgetApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.IntegrationTests.Budgeting;

public sealed class BudgetMonthSummaryServiceTests
{
    [Fact]
    public async Task GetCurrentMonthSummaryAsync_ReturnsBudgetedTotalsForLatestMonth()
    {
        var options = new DbContextOptionsBuilder<BudgetAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new BudgetAppDbContext(options);

        var housingGroup = new CategoryGroup
        {
            Id = Guid.NewGuid(),
            Name = "Housing",
            SortIndex = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var rentCategory = new Category
        {
            Id = Guid.NewGuid(),
            CategoryGroupId = housingGroup.Id,
            Name = "Rent",
            SortIndex = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var utilitiesCategory = new Category
        {
            Id = Guid.NewGuid(),
            CategoryGroupId = housingGroup.Id,
            Name = "Utilities",
            SortIndex = 2,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var priorMonth = new BudgetMonth
        {
            Id = Guid.NewGuid(),
            Month = new DateOnly(2026, 4, 1),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var currentMonth = new BudgetMonth
        {
            Id = Guid.NewGuid(),
            Month = new DateOnly(2026, 5, 1),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.CategoryGroups.Add(housingGroup);
        dbContext.Categories.AddRange(rentCategory, utilitiesCategory);
        dbContext.BudgetMonths.AddRange(priorMonth, currentMonth);
        dbContext.BudgetAllocations.AddRange(
            new BudgetAllocation
            {
                Id = Guid.NewGuid(),
                BudgetMonthId = priorMonth.Id,
                CategoryId = rentCategory.Id,
                Amount = 900m,
                Notes = "Old month",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new BudgetAllocation
            {
                Id = Guid.NewGuid(),
                BudgetMonthId = currentMonth.Id,
                CategoryId = rentCategory.Id,
                Amount = 1100m,
                Notes = "Current rent",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new BudgetAllocation
            {
                Id = Guid.NewGuid(),
                BudgetMonthId = currentMonth.Id,
                CategoryId = utilitiesCategory.Id,
                Amount = 250m,
                Notes = "Current utilities",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

        await dbContext.SaveChangesAsync();

        var service = new BudgetMonthSummaryService(dbContext);

        var summary = await service.GetCurrentMonthSummaryAsync(CancellationToken.None);

        Assert.NotNull(summary);
        Assert.Equal(new DateOnly(2026, 5, 1), summary!.Month);
        Assert.Equal(1350m, summary.TotalBudgeted);
        Assert.Equal(2, summary.CategoryCount);
        Assert.Equal(1, summary.GroupCount);
        Assert.Equal("Housing", summary.Groups[0].GroupName);
        Assert.Equal(1350m, summary.Groups[0].TotalBudgeted);
        Assert.Equal(2, summary.Groups[0].Categories.Count);
        Assert.Equal("Rent", summary.Groups[0].Categories[0].CategoryName);
        Assert.Equal(1100m, summary.Groups[0].Categories[0].Amount);
    }
}
