using BudgetApp.Domain.Entities;
using BudgetApp.Infrastructure.Budgeting;
using BudgetApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.IntegrationTests.Budgeting;

public sealed class TransactionCategorizationServiceTests
{
    [Fact]
    public async Task CategorizeTransactionAsync_AssignsExistingCategoryToTransaction()
    {
        var options = CreateOptions();
        await using var dbContext = new BudgetAppDbContext(options);
        var category = await SeedCategoryAsync(dbContext);
        var account = await SeedAccountAsync(dbContext);
        var transaction = BuildTransaction(account.Id, "Coffee shop");
        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync();

        var service = new TransactionCategorizationService(dbContext);

        var result = await service.CategorizeTransactionAsync(transaction.Id, category.Id, CancellationToken.None);

        Assert.Equal(transaction.Id, result.TransactionId);
        Assert.Equal(category.Id, result.CategoryId);
        Assert.Equal("Dining", result.CategoryName);
        Assert.Equal(category.Id, await dbContext.Transactions.Where(x => x.Id == transaction.Id).Select(x => x.CategoryId).SingleAsync());
    }

    [Fact]
    public async Task CategorizeTransactionAsync_ThrowsWhenTransactionDoesNotExist()
    {
        var options = CreateOptions();
        await using var dbContext = new BudgetAppDbContext(options);
        var category = await SeedCategoryAsync(dbContext);
        var service = new TransactionCategorizationService(dbContext);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CategorizeTransactionAsync(Guid.NewGuid(), category.Id, CancellationToken.None));

        Assert.Equal("Transaction was not found.", exception.Message);
    }

    [Fact]
    public async Task BulkCategorizeTransactionsAsync_AssignsFilteredUncategorizedPostedTransactions()
    {
        var options = CreateOptions();
        await using var dbContext = new BudgetAppDbContext(options);
        var category = await SeedCategoryAsync(dbContext);
        var account = await SeedAccountAsync(dbContext);
        var matching = BuildTransaction(account.Id, "Kroger Marketplace", -82.45m);
        var belowMinimum = BuildTransaction(account.Id, "Kroger Fuel", -12.00m);
        var differentMerchant = BuildTransaction(account.Id, "Gibson County Gas", -95.00m);
        var alreadyCategorized = BuildTransaction(account.Id, "Kroger Restaurant", -100.00m, category.Id);
        var pending = BuildTransaction(account.Id, "Kroger Pending", -125.00m, isPending: true);
        dbContext.Transactions.AddRange(matching, belowMinimum, differentMerchant, alreadyCategorized, pending);
        await dbContext.SaveChangesAsync();
        var service = new TransactionCategorizationService(dbContext);

        var result = await service.BulkCategorizeTransactionsAsync(
            new BulkCategorizeTransactionsRequest(category.Id, new TransactionReviewFilter(25, "kroger", 50m)),
            CancellationToken.None);

        Assert.Equal(category.Id, result.CategoryId);
        Assert.Equal(1, result.CategorizedCount);
        Assert.Contains(matching.Id, result.TransactionIds);
        Assert.Equal(category.Id, await dbContext.Transactions.Where(x => x.Id == matching.Id).Select(x => x.CategoryId).SingleAsync());
        Assert.Null(await dbContext.Transactions.Where(x => x.Id == belowMinimum.Id).Select(x => x.CategoryId).SingleAsync());
        Assert.Null(await dbContext.Transactions.Where(x => x.Id == differentMerchant.Id).Select(x => x.CategoryId).SingleAsync());
        Assert.Equal(category.Id, await dbContext.Transactions.Where(x => x.Id == alreadyCategorized.Id).Select(x => x.CategoryId).SingleAsync());
        Assert.Null(await dbContext.Transactions.Where(x => x.Id == pending.Id).Select(x => x.CategoryId).SingleAsync());
    }

    [Fact]
    public async Task BulkCategorizeTransactionsAsync_ThrowsWhenCategoryDoesNotExist()
    {
        var options = CreateOptions();
        await using var dbContext = new BudgetAppDbContext(options);
        var service = new TransactionCategorizationService(dbContext);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.BulkCategorizeTransactionsAsync(
                new BulkCategorizeTransactionsRequest(Guid.NewGuid(), new TransactionReviewFilter(25, null, null)),
                CancellationToken.None));

        Assert.Equal("Category was not found.", exception.Message);
    }

    [Fact]
    public async Task CategorizeTransactionAsync_ThrowsWhenCategoryDoesNotExist()
    {
        var options = CreateOptions();
        await using var dbContext = new BudgetAppDbContext(options);
        var account = await SeedAccountAsync(dbContext);
        var transaction = BuildTransaction(account.Id, "Coffee shop");
        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync();
        var service = new TransactionCategorizationService(dbContext);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CategorizeTransactionAsync(transaction.Id, Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("Category was not found.", exception.Message);
    }

    private static async Task<Category> SeedCategoryAsync(BudgetAppDbContext dbContext)
    {
        var group = new CategoryGroup
        {
            Id = Guid.NewGuid(),
            Name = "Food",
            SortIndex = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var category = new Category
        {
            Id = Guid.NewGuid(),
            CategoryGroupId = group.Id,
            Name = "Dining",
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
        string description,
        decimal amount = -6.50m,
        Guid? categoryId = null,
        bool isPending = false) => new()
    {
        Id = Guid.NewGuid(),
        AccountId = accountId,
        CategoryId = categoryId,
        Provider = "simplefin",
        ProviderConnectionId = "conn-1",
        ProviderTransactionId = Guid.NewGuid().ToString("N"),
        PostedAt = DateTimeOffset.UtcNow,
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
