using BudgetApp.Domain.Entities;
using BudgetApp.Infrastructure.Persistence;
using BudgetApp.Infrastructure.SimpleFin;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.IntegrationTests.SimpleFin;

public sealed class SimpleFinAccountSetImporterTests
{
    [Fact]
    public async Task ImportAsync_IsIdempotentAcrossRepeatedRuns()
    {
        var options = new DbContextOptionsBuilder<BudgetAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new BudgetAppDbContext(options);
        var connectionId = Guid.NewGuid();
        dbContext.SimpleFinConnections.Add(new SimpleFinConnection
        {
            Id = connectionId,
            Provider = "simplefin",
            AccessUrlCiphertext = "https://demo:secret@bridge.simplefin.org/simplefin",
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var importer = new SimpleFinAccountSetImporter(dbContext);

        var payload = new AccountSetResponse(
            [],
            [new ConnectionResponse("CON-1", "My Bank - Ethan", "ORG-1", "https://bank.example", "https://bank.example/simplefin")],
            [new AccountResponse(
                "CHK-1",
                "Checking",
                "CON-1",
                "USD",
                "1234.56",
                "1200.00",
                1_715_000_000,
                [new TransactionResponse("TX-1", 1_715_000_000, "-12.34", "Coffee Shop", null, false, null)])]);

        await importer.ImportAsync(connectionId, payload, CancellationToken.None);
        await importer.ImportAsync(connectionId, payload, CancellationToken.None);

        Assert.Equal(1, await dbContext.Accounts.CountAsync());
        Assert.Equal(1, await dbContext.Transactions.CountAsync());
        Assert.Equal(1, await dbContext.AccountBalanceSnapshots.CountAsync());
        Assert.Equal(connectionId, await dbContext.Accounts.Select(x => x.ConnectionId).SingleAsync());
    }

    [Fact]
    public async Task ImportAsync_TreatsSameAccountIdInDifferentConnectionsAsDistinctAccounts()
    {
        var options = new DbContextOptionsBuilder<BudgetAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new BudgetAppDbContext(options);
        var connectionId = Guid.NewGuid();
        dbContext.SimpleFinConnections.Add(new SimpleFinConnection
        {
            Id = connectionId,
            Provider = "simplefin",
            AccessUrlCiphertext = "https://demo:secret@bridge.simplefin.org/simplefin",
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var importer = new SimpleFinAccountSetImporter(dbContext);

        var payload = new AccountSetResponse(
            [],
            [
                new ConnectionResponse("CON-1", "My Bank - Personal", "ORG-1", "https://bank.example", "https://bank.example/simplefin"),
                new ConnectionResponse("CON-2", "My Bank - Joint", "ORG-1", "https://bank.example", "https://bank.example/simplefin")
            ],
            [
                new AccountResponse("CHK-1", "Checking", "CON-1", "USD", "100.00", "100.00", 1_715_000_000, []),
                new AccountResponse("CHK-1", "Checking", "CON-2", "USD", "250.00", "250.00", 1_715_000_000, [])
            ]);

        await importer.ImportAsync(connectionId, payload, CancellationToken.None);

        Assert.Equal(2, await dbContext.Accounts.CountAsync());
        Assert.Equal(2, await dbContext.Accounts.CountAsync(x => x.ConnectionId == connectionId));
    }
}
