using BudgetApp.Domain.Entities;
using BudgetApp.Infrastructure.Budgeting;
using BudgetApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.IntegrationTests.Budgeting;

public sealed class SpendingReportServiceTests
{
    [Fact]
    public async Task GetMonthlyReportAsync_ComparesBudgetedAndActualSpendingByCategory()
    {
        var options = CreateOptions();
        await using var dbContext = new BudgetAppDbContext(options);
        var housing = await SeedCategoryAsync(dbContext, "Housing", "Rent");
        var food = await SeedCategoryAsync(dbContext, "Food", "Groceries");
        var uncategorizedAccount = await SeedAccountAsync(dbContext);
        var month = new BudgetMonth
        {
            Id = Guid.NewGuid(),
            Month = new DateOnly(2026, 5, 1),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.BudgetMonths.Add(month);
        dbContext.BudgetAllocations.AddRange(
            new BudgetAllocation
            {
                Id = Guid.NewGuid(),
                BudgetMonthId = month.Id,
                CategoryId = housing.Id,
                Amount = 1500m,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new BudgetAllocation
            {
                Id = Guid.NewGuid(),
                BudgetMonthId = month.Id,
                CategoryId = food.Id,
                Amount = 600m,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        dbContext.Transactions.AddRange(
            BuildTransaction(uncategorizedAccount.Id, housing.Id, new DateTimeOffset(2026, 5, 3, 0, 0, 0, TimeSpan.Zero), -1500m, "Rent payment"),
            BuildTransaction(uncategorizedAccount.Id, food.Id, new DateTimeOffset(2026, 5, 5, 0, 0, 0, TimeSpan.Zero), -42.50m, "Grocery store"),
            BuildTransaction(uncategorizedAccount.Id, food.Id, new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero), 12.50m, "Grocery refund"),
            BuildTransaction(uncategorizedAccount.Id, null, new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero), -20m, "Uncategorized spend"),
            BuildTransaction(uncategorizedAccount.Id, housing.Id, new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), -1500m, "Next month rent"));
        await dbContext.SaveChangesAsync();

        var service = new SpendingReportService(dbContext);

        var report = await service.GetMonthlyReportAsync(new DateOnly(2026, 5, 1), CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 5, 1), report.Month);
        Assert.Equal(2100m, report.TotalBudgeted);
        Assert.Equal(1530m, report.TotalSpent);
        Assert.Equal(570m, report.TotalRemaining);
        Assert.Equal(20m, report.UncategorizedSpent);

        var rent = Assert.Single(report.Groups.Single(x => x.GroupName == "Housing").Categories);
        Assert.Equal("Rent", rent.CategoryName);
        Assert.Equal(1500m, rent.Budgeted);
        Assert.Equal(1500m, rent.Spent);
        Assert.Equal(0m, rent.Remaining);

        var groceries = Assert.Single(report.Groups.Single(x => x.GroupName == "Food").Categories);
        Assert.Equal("Groceries", groceries.CategoryName);
        Assert.Equal(600m, groceries.Budgeted);
        Assert.Equal(30m, groceries.Spent);
        Assert.Equal(570m, groceries.Remaining);
    }

    private static async Task<Category> SeedCategoryAsync(BudgetAppDbContext dbContext, string groupName, string categoryName)
    {
        var group = new CategoryGroup
        {
            Id = Guid.NewGuid(),
            Name = groupName,
            SortIndex = await dbContext.CategoryGroups.CountAsync() + 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var category = new Category
        {
            Id = Guid.NewGuid(),
            CategoryGroupId = group.Id,
            Name = categoryName,
            SortIndex = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.CategoryGroups.Add(group);
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync();
        return category;
    }

    private static async Task<Account> SeedAccountAsync(BudgetAppDbContext dbContext)
    {
        var institution = new Institution
        {
            Id = Guid.NewGuid(),
            Provider = "simplefin",
            ProviderInstitutionId = "inst-1",
            Name = "Bank",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var account = new Account
        {
            Id = Guid.NewGuid(),
            InstitutionId = institution.Id,
            Provider = "simplefin",
            ProviderConnectionId = "conn-1",
            ProviderAccountId = "acct-1",
            Name = "Checking",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Institutions.Add(institution);
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();
        return account;
    }

    private static Transaction BuildTransaction(
        Guid accountId,
        Guid? categoryId,
        DateTimeOffset postedAt,
        decimal amount,
        string description) => new()
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            CategoryId = categoryId,
            Provider = "simplefin",
            ProviderConnectionId = "conn-1",
            ProviderTransactionId = Guid.NewGuid().ToString("N"),
            PostedAt = postedAt,
            Amount = amount,
            Description = description,
            ImportedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static DbContextOptions<BudgetAppDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<BudgetAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
}
