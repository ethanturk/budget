using BudgetApp.Domain.Entities;
using BudgetApp.Infrastructure.Persistence;
using BudgetApp.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.IntegrationTests.Dashboard;

public sealed class AccountTransactionQueryServiceTests
{
    [Fact]
    public async Task GetAccountTransactionsAsync_ReturnsAccountHeaderAndTransactionsForOnlyThatAccount()
    {
        var options = new DbContextOptionsBuilder<BudgetAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new BudgetAppDbContext(options);
        var institution = new Institution
        {
            Id = Guid.NewGuid(),
            Provider = "simplefin",
            ProviderInstitutionId = "BANK-1",
            Name = "Checking Bank",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var account = BuildAccount(institution.Id, "Primary Checking", "CHK-1");
        var otherAccount = BuildAccount(institution.Id, "Savings", "SAV-1");
        dbContext.Institutions.Add(institution);
        dbContext.Accounts.AddRange(account, otherAccount);
        dbContext.AccountBalanceSnapshots.Add(new AccountBalanceSnapshot
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            AsOfAt = DateTimeOffset.UtcNow,
            CurrentAmount = 1500m,
            AvailableAmount = 1450m,
            CurrencyCode = "USD"
        });
        dbContext.Transactions.AddRange(
            BuildTransaction(account.Id, "TX-1", "Coffee", -5.25m, DateTimeOffset.UtcNow.AddDays(-1), false),
            BuildTransaction(account.Id, "TX-2", "Pending gas", -35.10m, DateTimeOffset.UtcNow, true),
            BuildTransaction(otherAccount.Id, "TX-3", "Should not appear", -100m, DateTimeOffset.UtcNow, false));
        await dbContext.SaveChangesAsync();

        var service = new AccountTransactionQueryService(dbContext);

        var detail = await service.GetAccountTransactionsAsync(account.Id, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(account.Id, detail!.AccountId);
        Assert.Equal("Primary Checking", detail.AccountName);
        Assert.Equal("Checking Bank", detail.InstitutionName);
        Assert.Equal(1500m, detail.CurrentBalance);
        Assert.Equal(1450m, detail.AvailableBalance);
        Assert.Equal(2, detail.Transactions.Count);
        Assert.Equal("Pending gas", detail.Transactions[0].Description);
        Assert.True(detail.Transactions[0].IsPending);
        Assert.Equal("Coffee", detail.Transactions[1].Description);
        Assert.DoesNotContain(detail.Transactions, x => x.Description == "Should not appear");
    }

    [Fact]
    public async Task GetAccountTransactionsAsync_ReturnsNullForUnknownAccount()
    {
        var options = new DbContextOptionsBuilder<BudgetAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new BudgetAppDbContext(options);
        var service = new AccountTransactionQueryService(dbContext);

        var detail = await service.GetAccountTransactionsAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(detail);
    }

    private static Account BuildAccount(Guid institutionId, string name, string providerAccountId) => new()
    {
        Id = Guid.NewGuid(),
        InstitutionId = institutionId,
        Provider = "simplefin",
        ProviderConnectionId = "CON-1",
        ProviderAccountId = providerAccountId,
        Name = name,
        CurrencyCode = "USD",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static Transaction BuildTransaction(Guid accountId, string providerTransactionId, string description, decimal amount, DateTimeOffset postedAt, bool isPending) => new()
    {
        Id = Guid.NewGuid(),
        AccountId = accountId,
        Provider = "simplefin",
        ProviderConnectionId = "CON-1",
        ProviderTransactionId = providerTransactionId,
        PostedAt = postedAt,
        Amount = amount,
        Description = description,
        IsPending = isPending,
        ImportedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };
}
