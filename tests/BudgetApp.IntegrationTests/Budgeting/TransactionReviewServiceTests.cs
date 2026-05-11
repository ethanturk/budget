using BudgetApp.Domain.Entities;
using BudgetApp.Infrastructure.Budgeting;
using BudgetApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.IntegrationTests.Budgeting;

public sealed class TransactionReviewServiceTests
{
    [Fact]
    public async Task GetUncategorizedTransactionsAsync_ReturnsRecentPostedTransactionsWithCategoryOptions()
    {
        var options = CreateOptions();
        await using var dbContext = new BudgetAppDbContext(options);
        var category = await SeedCategoryAsync(dbContext, "Food", "Dining");
        var account = await SeedAccountAsync(dbContext);
        dbContext.Transactions.AddRange(
            BuildTransaction(account.Id, null, -12.34m, "Coffee", new DateTimeOffset(2026, 5, 3, 0, 0, 0, TimeSpan.Zero)),
            BuildTransaction(account.Id, category.Id, -42.00m, "Restaurant", new DateTimeOffset(2026, 5, 2, 0, 0, 0, TimeSpan.Zero)),
            BuildTransaction(account.Id, null, -8.00m, "Pending coffee", new DateTimeOffset(2026, 5, 4, 0, 0, 0, TimeSpan.Zero), isPending: true));
        await dbContext.SaveChangesAsync();
        var service = new TransactionReviewService(dbContext);

        var review = await service.GetUncategorizedTransactionsAsync(10, CancellationToken.None);

        var transaction = Assert.Single(review.Transactions);
        Assert.Equal("Coffee", transaction.Description);
        Assert.Equal(12.34m, transaction.SpendingAmount);
        var option = Assert.Single(review.CategoryOptions);
        Assert.Equal(category.Id, option.CategoryId);
        Assert.Equal("Food", option.GroupName);
        Assert.Equal("Dining", option.CategoryName);
    }

    private static async Task<Category> SeedCategoryAsync(BudgetAppDbContext dbContext, string groupName, string categoryName)
    {
        var group = new CategoryGroup
        {
            Id = Guid.NewGuid(),
            Name = groupName,
            SortIndex = 1,
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

    private static Transaction BuildTransaction(Guid accountId, Guid? categoryId, decimal amount, string description, DateTimeOffset postedAt, bool isPending = false) => new()
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
