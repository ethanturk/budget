using BudgetApp.Domain.Entities;
using BudgetApp.Infrastructure.Persistence;
using BudgetApp.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.IntegrationTests.Dashboard;

public sealed class DashboardQueryServiceTests
{
    [Fact]
    public async Task GetDashboardAsync_ReturnsConnectionSyncAndTransactionSummaries()
    {
        var options = new DbContextOptionsBuilder<BudgetAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new BudgetAppDbContext(options);

        var connection = new SimpleFinConnection
        {
            Id = Guid.NewGuid(),
            Provider = "simplefin",
            AccessUrlCiphertext = "https://demo:secret@bridge.simplefin.org/simplefin",
            AccessUrlHint = "bridge.simplefin.org",
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-5),
            UpdatedAt = DateTimeOffset.UtcNow,
            LastAttemptedSyncAt = DateTimeOffset.UtcNow.AddMinutes(-20),
            LastSuccessfulSyncAt = DateTimeOffset.UtcNow.AddMinutes(-19)
        };

        var institution = new Institution
        {
            Id = Guid.NewGuid(),
            Provider = "simplefin",
            ProviderInstitutionId = "ORG-1",
            Name = "Checking Bank",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-5),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var account = new Account
        {
            Id = Guid.NewGuid(),
            ConnectionId = connection.Id,
            InstitutionId = institution.Id,
            Provider = "simplefin",
            ProviderConnectionId = "CON-1",
            ProviderAccountId = "CHK-1",
            Name = "Primary Checking",
            CurrencyCode = "USD",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-5),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var snapshot = new AccountBalanceSnapshot
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            AsOfAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            CurrencyCode = "USD",
            CurrentAmount = 2500.12m,
            AvailableAmount = 2400.12m
        };

        var olderRun = new SyncRun
        {
            Id = Guid.NewGuid(),
            ConnectionId = connection.Id,
            TriggerSource = "manual",
            Status = "failed",
            StartedAt = DateTimeOffset.UtcNow.AddHours(-3),
            CompletedAt = DateTimeOffset.UtcNow.AddHours(-3).AddMinutes(1),
            AccountsSeen = 0,
            TransactionsSeen = 0,
            TransactionsInserted = 0,
            TransactionsUpdated = 0,
            ErrorText = "Bridge timeout"
        };

        var syncRun = new SyncRun
        {
            Id = Guid.NewGuid(),
            ConnectionId = connection.Id,
            TriggerSource = "manual",
            Status = "succeeded",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-20),
            CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-19),
            AccountsSeen = 1,
            TransactionsSeen = 2,
            TransactionsInserted = 2,
            TransactionsUpdated = 0
        };

        var expense = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            Provider = "simplefin",
            ProviderConnectionId = "CON-1",
            ProviderTransactionId = "TX-1",
            PostedAt = DateTimeOffset.UtcNow.AddDays(-1),
            Amount = -12.34m,
            Description = "Coffee Shop",
            ImportedAt = DateTimeOffset.UtcNow.AddMinutes(-19),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-19)
        };

        var income = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            Provider = "simplefin",
            ProviderConnectionId = "CON-1",
            ProviderTransactionId = "TX-2",
            PostedAt = DateTimeOffset.UtcNow.AddHours(-3),
            Amount = 1500m,
            Description = "Payroll",
            ImportedAt = DateTimeOffset.UtcNow.AddMinutes(-19),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-19)
        };

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

        var currentMonth = new BudgetMonth
        {
            Id = Guid.NewGuid(),
            Month = new DateOnly(2026, 5, 1),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var allocation = new BudgetAllocation
        {
            Id = Guid.NewGuid(),
            BudgetMonthId = currentMonth.Id,
            CategoryId = rentCategory.Id,
            Amount = 1100m,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.SimpleFinConnections.Add(connection);
        dbContext.Institutions.Add(institution);
        dbContext.Accounts.Add(account);
        dbContext.AccountBalanceSnapshots.Add(snapshot);
        dbContext.SyncRuns.AddRange(olderRun, syncRun);
        dbContext.Transactions.AddRange(expense, income);
        dbContext.CategoryGroups.Add(housingGroup);
        dbContext.Categories.Add(rentCategory);
        dbContext.BudgetMonths.Add(currentMonth);
        dbContext.BudgetAllocations.Add(allocation);
        await dbContext.SaveChangesAsync();

        var service = new DashboardQueryService(dbContext);

        var dashboard = await service.GetDashboardAsync(CancellationToken.None);

        Assert.Equal(1, dashboard.ConnectionCount);
        Assert.Equal(1, dashboard.InstitutionCount);
        Assert.Equal(1, dashboard.AccountCount);
        Assert.Equal(2500.12m, dashboard.TotalCurrentBalance);
        Assert.Equal(new DateOnly(2026, 5, 1), dashboard.Budget.Month);
        Assert.Equal(1100m, dashboard.Budget.TotalBudgeted);
        Assert.Equal(1, dashboard.Budget.BudgetGroupCount);
        Assert.Equal(1, dashboard.Budget.BudgetCategoryCount);
        Assert.NotNull(dashboard.LatestSync);
        Assert.Equal("succeeded", dashboard.LatestSync!.Status);
        Assert.Equal(2, dashboard.RecentTransactions.Count);
        Assert.Equal("Payroll", dashboard.RecentTransactions[0].Description);
        Assert.Equal("Primary Checking", dashboard.RecentTransactions[0].AccountName);
        Assert.Single(dashboard.Connections);
        Assert.Equal("Checking Bank", dashboard.Connections[0].InstitutionName);
        Assert.Equal(2500.12m, dashboard.Connections[0].CurrentBalance);
        Assert.Equal(2, dashboard.RecentSyncRuns.Count);
        Assert.Equal("succeeded", dashboard.RecentSyncRuns[0].Status);
        Assert.Equal("failed", dashboard.RecentSyncRuns[1].Status);
        Assert.Equal("Bridge timeout", dashboard.RecentSyncRuns[1].ErrorText);
    }
}
