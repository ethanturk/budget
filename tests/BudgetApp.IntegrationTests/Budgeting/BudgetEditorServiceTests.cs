using BudgetApp.Infrastructure.Budgeting;
using BudgetApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.IntegrationTests.Budgeting;

public sealed class BudgetEditorServiceTests
{
    [Fact]
    public async Task SaveAllocationAsync_CreatesMonthGroupCategoryAndAllocation()
    {
        var options = new DbContextOptionsBuilder<BudgetAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new BudgetAppDbContext(options);
        var service = new BudgetEditorService(dbContext);

        var result = await service.SaveAllocationAsync(new SaveBudgetAllocationRequest(
            new DateOnly(2026, 5, 1),
            "Housing",
            "Rent",
            1250m), CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 5, 1), result.Month);
        Assert.Equal("Housing", result.GroupName);
        Assert.Equal("Rent", result.CategoryName);
        Assert.Equal(1250m, result.Amount);

        Assert.Equal(1, await dbContext.BudgetMonths.CountAsync());
        Assert.Equal(1, await dbContext.CategoryGroups.CountAsync());
        Assert.Equal(1, await dbContext.Categories.CountAsync());
        Assert.Equal(1, await dbContext.BudgetAllocations.CountAsync());
    }

    [Fact]
    public async Task SaveAllocationAsync_UpdatesExistingAllocationWithoutDuplicates()
    {
        var options = new DbContextOptionsBuilder<BudgetAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new BudgetAppDbContext(options);
        var service = new BudgetEditorService(dbContext);

        await service.SaveAllocationAsync(new SaveBudgetAllocationRequest(
            new DateOnly(2026, 5, 1),
            "Housing",
            "Rent",
            1250m), CancellationToken.None);

        var updated = await service.SaveAllocationAsync(new SaveBudgetAllocationRequest(
            new DateOnly(2026, 5, 1),
            "Housing",
            "Rent",
            1400m), CancellationToken.None);

        Assert.Equal(1400m, updated.Amount);
        Assert.Equal(1, await dbContext.BudgetMonths.CountAsync());
        Assert.Equal(1, await dbContext.CategoryGroups.CountAsync());
        Assert.Equal(1, await dbContext.Categories.CountAsync());
        Assert.Equal(1, await dbContext.BudgetAllocations.CountAsync());
    }
}
