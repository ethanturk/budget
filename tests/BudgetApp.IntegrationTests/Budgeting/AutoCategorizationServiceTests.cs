using BudgetApp.Domain.Entities;
using BudgetApp.Infrastructure.Budgeting;
using BudgetApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.IntegrationTests.Budgeting;

public sealed class AutoCategorizationServiceTests
{
    [Fact]
    public async Task ApplyRulesAsync_AssignsMatchingActiveRulesToUncategorizedTransactions()
    {
        var options = CreateOptions();
        await using var dbContext = new BudgetAppDbContext(options);
        var groceries = await SeedCategoryAsync(dbContext, "Food", "Groceries");
        var dining = await SeedCategoryAsync(dbContext, "Food", "Dining");
        var account = await SeedAccountAsync(dbContext);
        dbContext.CategoryRules.AddRange(
            BuildRule(groceries.Id, "KROGER"),
            BuildRule(dining.Id, "COFFEE", isActive: false));
        var matching = BuildTransaction(account.Id, "Kroger Fuel Center", -47.25m);
        var inactiveRuleMatch = BuildTransaction(account.Id, "Coffee Shop", -6.10m);
        var alreadyCategorized = BuildTransaction(account.Id, "Kroger Marketplace", -21.00m, dining.Id);
        var pending = BuildTransaction(account.Id, "Kroger Pending", -10.00m, isPending: true);
        dbContext.Transactions.AddRange(matching, inactiveRuleMatch, alreadyCategorized, pending);
        await dbContext.SaveChangesAsync();

        var service = new AutoCategorizationService(dbContext);

        var result = await service.ApplyRulesAsync(CancellationToken.None);

        Assert.Equal(1, result.CategorizedCount);
        Assert.Contains(result.Matches, x => x.TransactionId == matching.Id && x.CategoryId == groceries.Id && x.MatchText == "KROGER");
        Assert.Equal(groceries.Id, await dbContext.Transactions.Where(x => x.Id == matching.Id).Select(x => x.CategoryId).SingleAsync());
        Assert.Null(await dbContext.Transactions.Where(x => x.Id == inactiveRuleMatch.Id).Select(x => x.CategoryId).SingleAsync());
        Assert.Equal(dining.Id, await dbContext.Transactions.Where(x => x.Id == alreadyCategorized.Id).Select(x => x.CategoryId).SingleAsync());
        Assert.Null(await dbContext.Transactions.Where(x => x.Id == pending.Id).Select(x => x.CategoryId).SingleAsync());
    }

    [Fact]
    public async Task SaveRuleAsync_CreatesRuleForExistingCategory()
    {
        var options = CreateOptions();
        await using var dbContext = new BudgetAppDbContext(options);
        var category = await SeedCategoryAsync(dbContext, "Utilities", "Gas");
        var service = new AutoCategorizationService(dbContext);

        var result = await service.SaveRuleAsync(new SaveCategoryRuleRequest(category.Id, "GIBSONCOUNT"), CancellationToken.None);

        Assert.Equal(category.Id, result.CategoryId);
        Assert.Equal("GIBSONCOUNT", result.MatchText);
        Assert.True(await dbContext.CategoryRules.AnyAsync(x => x.Id == result.RuleId && x.IsActive));
    }

    [Fact]
    public async Task SaveRuleAsync_RejectsBlankMatchText()
    {
        var options = CreateOptions();
        await using var dbContext = new BudgetAppDbContext(options);
        var category = await SeedCategoryAsync(dbContext, "Utilities", "Gas");
        var service = new AutoCategorizationService(dbContext);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveRuleAsync(new SaveCategoryRuleRequest(category.Id, " "), CancellationToken.None));

        Assert.Equal("Rule match text is required.", exception.Message);
    }

    [Fact]
    public async Task GetRulesAsync_ReturnsRulesWithCategoryAndGroupNames()
    {
        var options = CreateOptions();
        await using var dbContext = new BudgetAppDbContext(options);
        var groceries = await SeedCategoryAsync(dbContext, "Food", "Groceries");
        var gas = await SeedCategoryAsync(dbContext, "Utilities", "Gas");
        dbContext.CategoryRules.AddRange(
            BuildRule(groceries.Id, "KROGER"),
            BuildRule(gas.Id, "GIBSONCOUNT", isActive: false));
        await dbContext.SaveChangesAsync();
        var service = new AutoCategorizationService(dbContext);

        var rules = await service.GetRulesAsync(CancellationToken.None);

        Assert.Collection(
            rules,
            rule =>
            {
                Assert.Equal("Food", rule.GroupName);
                Assert.Equal("Groceries", rule.CategoryName);
                Assert.Equal("KROGER", rule.MatchText);
                Assert.True(rule.IsActive);
            },
            rule =>
            {
                Assert.Equal("Utilities", rule.GroupName);
                Assert.Equal("Gas", rule.CategoryName);
                Assert.Equal("GIBSONCOUNT", rule.MatchText);
                Assert.False(rule.IsActive);
            });
    }

    [Fact]
    public async Task DeactivateRuleAsync_MarksRuleInactiveWithoutDeletingIt()
    {
        var options = CreateOptions();
        await using var dbContext = new BudgetAppDbContext(options);
        var category = await SeedCategoryAsync(dbContext, "Food", "Groceries");
        var rule = BuildRule(category.Id, "KROGER");
        dbContext.CategoryRules.Add(rule);
        await dbContext.SaveChangesAsync();
        var service = new AutoCategorizationService(dbContext);

        await service.DeactivateRuleAsync(rule.Id, CancellationToken.None);

        var savedRule = await dbContext.CategoryRules.SingleAsync(x => x.Id == rule.Id);
        Assert.False(savedRule.IsActive);
        Assert.True(savedRule.UpdatedAt >= rule.UpdatedAt);
    }

    private static CategoryRule BuildRule(Guid categoryId, string matchText, bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        CategoryId = categoryId,
        MatchText = matchText,
        IsActive = isActive,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static async Task<Category> SeedCategoryAsync(BudgetAppDbContext dbContext, string groupName, string categoryName)
    {
        var group = await dbContext.CategoryGroups.FirstOrDefaultAsync(x => x.Name == groupName);
        if (group is null)
        {
            group = new CategoryGroup
            {
                Id = Guid.NewGuid(),
                Name = groupName,
                SortIndex = await dbContext.CategoryGroups.CountAsync() + 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            dbContext.CategoryGroups.Add(group);
            await dbContext.SaveChangesAsync();
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            CategoryGroupId = group.Id,
            Name = categoryName,
            SortIndex = await dbContext.Categories.CountAsync(x => x.CategoryGroupId == group.Id) + 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
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

    private static Transaction BuildTransaction(Guid accountId, string description, decimal amount, Guid? categoryId = null, bool isPending = false) => new()
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
