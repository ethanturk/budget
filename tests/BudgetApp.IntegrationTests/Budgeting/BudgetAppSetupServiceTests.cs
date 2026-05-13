using BudgetApp.Infrastructure.Budgeting;
using BudgetApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.IntegrationTests.Budgeting;

public sealed class BudgetAppSetupServiceTests
{
    [Fact]
    public async Task InitializeAsync_SeedsDefaultCategoryGroupsAndCategoriesIdempotently()
    {
        var options = CreateOptions();
        await using var dbContext = new BudgetAppDbContext(options);
        var categoryManagementService = new CategoryManagementService(dbContext);
        var setupService = new BudgetAppSetupService(dbContext, categoryManagementService);

        await setupService.InitializeAsync(CancellationToken.None);
        await setupService.InitializeAsync(CancellationToken.None);

        var catalog = await categoryManagementService.GetCatalogAsync(CancellationToken.None);

        Assert.Equal(new[] { "Transportation", "Food", "Tech", "Shopping" }, catalog.Groups.Select(x => x.GroupName));
        Assert.Equal(new[] { "Auto Loan", "Gas", "Miscellaneous" }, catalog.Groups[0].Categories.Select(x => x.CategoryName));
        Assert.Equal(new[] { "Fast Food", "Groceries", "Restaurants" }, catalog.Groups[1].Categories.Select(x => x.CategoryName));
        Assert.Equal(new[] { "AI", "Hardware", "Subscriptions", "Miscellaneous" }, catalog.Groups[2].Categories.Select(x => x.CategoryName));
        Assert.Equal(new[] { "Miscellaneous", "Gifts" }, catalog.Groups[3].Categories.Select(x => x.CategoryName));

        Assert.Equal(4, await dbContext.CategoryGroups.CountAsync());
        Assert.Equal(12, await dbContext.Categories.CountAsync());
    }

    private static DbContextOptions<BudgetAppDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<BudgetAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
}
