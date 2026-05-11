using BudgetApp.Infrastructure.Budgeting;
using BudgetApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.IntegrationTests.Budgeting;

public sealed class CategoryManagementServiceTests
{
    [Fact]
    public async Task SaveCategoryAsync_CreatesGroupAndCategoryInCatalogOrder()
    {
        var options = CreateOptions();
        await using var dbContext = new BudgetAppDbContext(options);
        var service = new CategoryManagementService(dbContext);

        await service.SaveCategoryAsync(new SaveCategoryRequest("Housing", "Rent"), CancellationToken.None);
        await service.SaveCategoryAsync(new SaveCategoryRequest("Housing", "Utilities"), CancellationToken.None);
        await service.SaveCategoryAsync(new SaveCategoryRequest("Food", "Groceries"), CancellationToken.None);

        var catalog = await service.GetCatalogAsync(CancellationToken.None);

        Assert.Equal(2, catalog.Groups.Count);
        Assert.Equal("Housing", catalog.Groups[0].GroupName);
        Assert.Equal(new[] { "Rent", "Utilities" }, catalog.Groups[0].Categories.Select(x => x.CategoryName));
        Assert.Equal("Food", catalog.Groups[1].GroupName);
        Assert.Equal(new[] { "Groceries" }, catalog.Groups[1].Categories.Select(x => x.CategoryName));
    }

    [Fact]
    public async Task SaveCategoryAsync_ReactivatesArchivedCategoryWithoutCreatingDuplicates()
    {
        var options = CreateOptions();
        await using var dbContext = new BudgetAppDbContext(options);
        var service = new CategoryManagementService(dbContext);

        var created = await service.SaveCategoryAsync(new SaveCategoryRequest("Housing", "Rent"), CancellationToken.None);
        await service.ArchiveCategoryAsync(created.CategoryId, CancellationToken.None);

        var reactivated = await service.SaveCategoryAsync(new SaveCategoryRequest("Housing", "Rent"), CancellationToken.None);

        Assert.Equal(created.CategoryId, reactivated.CategoryId);
        Assert.False(await dbContext.Categories.Where(x => x.Id == created.CategoryId).Select(x => x.IsArchived).SingleAsync());
        Assert.Equal(1, await dbContext.CategoryGroups.CountAsync());
        Assert.Equal(1, await dbContext.Categories.CountAsync());
    }

    [Fact]
    public async Task ArchiveCategoryAsync_HidesCategoryFromActiveCatalogButKeepsHistory()
    {
        var options = CreateOptions();
        await using var dbContext = new BudgetAppDbContext(options);
        var service = new CategoryManagementService(dbContext);

        var created = await service.SaveCategoryAsync(new SaveCategoryRequest("Housing", "Rent"), CancellationToken.None);

        await service.ArchiveCategoryAsync(created.CategoryId, CancellationToken.None);
        var catalog = await service.GetCatalogAsync(CancellationToken.None);

        Assert.Empty(catalog.Groups);
        Assert.True(await dbContext.Categories.Where(x => x.Id == created.CategoryId).Select(x => x.IsArchived).SingleAsync());
    }

    private static DbContextOptions<BudgetAppDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<BudgetAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
}
