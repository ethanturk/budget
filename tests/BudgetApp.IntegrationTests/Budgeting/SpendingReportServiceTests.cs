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

    [Fact]
    public async Task GetMonthlyReportAsync_AddsSpendingStatusAndPercentSpent()
    {
        var options = CreateOptions();
        await using var dbContext = new BudgetAppDbContext(options);
        var dining = await SeedCategoryAsync(dbContext, "Food", "Dining");
        var fuel = await SeedCategoryAsync(dbContext, "Transportation", "Fuel");
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
                CategoryId = dining.Id,
                Amount = 100m,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new BudgetAllocation
            {
                Id = Guid.NewGuid(),
                BudgetMonthId = month.Id,
                CategoryId = fuel.Id,
                Amount = 100m,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        var account = await SeedAccountAsync(dbContext);
        dbContext.Transactions.AddRange(
            BuildTransaction(
                account.Id,
                dining.Id,
                new DateTimeOffset(2026, 5, 8, 0, 0, 0, TimeSpan.Zero),
                -125m,
                "Restaurant"),
            BuildTransaction(
                account.Id,
                fuel.Id,
                new DateTimeOffset(2026, 5, 9, 0, 0, 0, TimeSpan.Zero),
                -90m,
                "Gas station"));
        await dbContext.SaveChangesAsync();
        var service = new SpendingReportService(dbContext);

        var report = await service.GetMonthlyReportAsync(new DateOnly(2026, 5, 1), CancellationToken.None);

        var diningCategory = Assert.Single(report.Groups.Single(x => x.GroupName == "Food").Categories);
        Assert.Equal(125m, diningCategory.PercentSpent);
        Assert.Equal(SpendingStatus.OverBudget, diningCategory.Status);

        var fuelCategory = Assert.Single(report.Groups.Single(x => x.GroupName == "Transportation").Categories);
        Assert.Equal(90m, fuelCategory.PercentSpent);
        Assert.Equal(SpendingStatus.NearLimit, fuelCategory.Status);
    }

    [Fact]
    public async Task GetMonthlyReportAsync_IncludesPostedCategoryTransactionsForDrilldown()
    {
        var options = CreateOptions();
        await using var dbContext = new BudgetAppDbContext(options);
        var groceries = await SeedCategoryAsync(dbContext, "Food", "Groceries");
        var dining = await SeedCategoryAsync(dbContext, "Food", "Dining");
        var account = await SeedAccountAsync(dbContext);
        dbContext.Transactions.AddRange(
            BuildTransaction(account.Id, groceries.Id, new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero), -30m, "Grocery store"),
            BuildTransaction(account.Id, groceries.Id, new DateTimeOffset(2026, 5, 11, 0, 0, 0, TimeSpan.Zero), -12.34m, "Farmers market"),
            BuildTransaction(account.Id, groceries.Id, new DateTimeOffset(2026, 5, 12, 0, 0, 0, TimeSpan.Zero), -99m, "Pending grocery", isPending: true),
            BuildTransaction(account.Id, groceries.Id, new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), -45m, "June grocery"),
            BuildTransaction(account.Id, dining.Id, new DateTimeOffset(2026, 5, 13, 0, 0, 0, TimeSpan.Zero), -20m, "Restaurant"));
        await dbContext.SaveChangesAsync();
        var service = new SpendingReportService(dbContext);

        var report = await service.GetMonthlyReportAsync(new DateOnly(2026, 5, 1), CancellationToken.None);

        var category = report.Groups.SelectMany(x => x.Categories).Single(x => x.CategoryName == "Groceries");
        Assert.Equal(2, category.Transactions.Count);
        Assert.Collection(category.Transactions,
            transaction =>
            {
                Assert.Equal(new DateTimeOffset(2026, 5, 11, 0, 0, 0, TimeSpan.Zero), transaction.PostedAt);
                Assert.Equal("Farmers market", transaction.Description);
                Assert.Equal(-12.34m, transaction.Amount);
                Assert.Equal("Checking", transaction.AccountName);
            },
            transaction =>
            {
                Assert.Equal(new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero), transaction.PostedAt);
                Assert.Equal("Grocery store", transaction.Description);
                Assert.Equal(-30m, transaction.Amount);
                Assert.Equal("Checking", transaction.AccountName);
            });
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
        string description,
        bool isPending = false) => new()
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
            IsPending = isPending,
            ImportedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static DbContextOptions<BudgetAppDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<BudgetAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
}
