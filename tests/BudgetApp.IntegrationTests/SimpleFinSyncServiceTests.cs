using System.Net;
using BudgetApp.Domain.Entities;
using BudgetApp.Infrastructure.Budgeting;
using BudgetApp.Infrastructure.Persistence;
using BudgetApp.Infrastructure.SimpleFin;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.IntegrationTests.SimpleFin;

public sealed class SimpleFinSyncServiceTests
{
    [Fact]
    public async Task RunSyncAsync_ImportsAccountsAndTransactions_AndRecordsSuccessfulRun()
    {
        var dbOptions = new DbContextOptionsBuilder<BudgetAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new BudgetAppDbContext(dbOptions);
        var connection = new SimpleFinConnection
        {
            Id = Guid.NewGuid(),
            Provider = "simplefin",
            AccessUrlCiphertext = "https://demo:secret@bridge.simplefin.org/simplefin",
            AccessUrlHint = "bridge.simplefin.org",
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.SimpleFinConnections.Add(connection);
        await dbContext.SaveChangesAsync();

        Uri? requestedUri = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            requestedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "errlist": [],
              "connections": [
                {
                  "conn_id": "CON-1",
                  "name": "Checking Bank",
                  "org_id": "ORG-1",
                  "org_url": "https://bank.example",
                  "sfin_url": "https://bank.example/simplefin"
                }
              ],
              "accounts": [
                {
                  "id": "CHK-1",
                  "name": "Primary Checking",
                  "conn_id": "CON-1",
                  "currency": "USD",
                  "balance": "2500.12",
                  "available-balance": "2400.12",
                  "balance-date": 1715000000,
                  "transactions": [
                    {
                      "id": "TX-1",
                      "posted": 1715000000,
                      "amount": "-12.34",
                      "description": "Coffee Shop",
                      "payee": "Coffee Shop LLC",
                      "memo": "Latte and bagel",
                      "pending": false
                    },
                    {
                      "id": "TX-2",
                      "posted": 1715086400,
                      "amount": "1500.00",
                      "description": "Payroll",
                      "pending": false
                    }
                  ]
                }
              ]
            }
            """)
            };
        }));

        var client = new SimpleFinClient(httpClient);
        var importer = new SimpleFinAccountSetImporter(dbContext);
        var autoCategorizationService = new AutoCategorizationService(dbContext);
        var service = new SimpleFinSyncService(dbContext, client, importer, autoCategorizationService);

        var result = await service.RunSyncAsync(connection.Id, CancellationToken.None);

        Assert.Equal("succeeded", result.Status);
        Assert.Equal(1, result.AccountsSeen);
        Assert.Equal(2, result.TransactionsSeen);
        Assert.Equal(2, result.TransactionsInserted);
        Assert.Equal(0, result.TransactionsUpdated);

        var reloadedConnection = await dbContext.SimpleFinConnections.SingleAsync();
        Assert.NotNull(reloadedConnection.LastAttemptedSyncAt);
        Assert.NotNull(reloadedConnection.LastSuccessfulSyncAt);
        Assert.Null(reloadedConnection.LastError);

        var run = await dbContext.SyncRuns.SingleAsync();
        Assert.Equal("manual", run.TriggerSource);
        Assert.Equal("succeeded", run.Status);
        Assert.Equal(1, run.AccountsSeen);
        Assert.Equal(2, run.TransactionsSeen);

        Assert.Equal(1, await dbContext.Accounts.CountAsync());
        Assert.Equal(2, await dbContext.Transactions.CountAsync());
        var coffeeTransaction = await dbContext.Transactions.SingleAsync(x => x.ProviderTransactionId == "TX-1");
        Assert.Equal("Coffee Shop LLC", coffeeTransaction.Payee);
        Assert.Equal("Latte and bagel", coffeeTransaction.Memo);
        Assert.NotNull(requestedUri);
        Assert.Contains("version=2", requestedUri!.Query);
        Assert.Contains("start-date=", requestedUri.Query);
        Assert.Contains("end-date=", requestedUri.Query);
    }

    [Fact]
    public async Task RunSyncAsync_AppliesActiveCategoryRulesToImportedTransactions()
    {
        var dbOptions = new DbContextOptionsBuilder<BudgetAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new BudgetAppDbContext(dbOptions);
        var connection = new SimpleFinConnection
        {
            Id = Guid.NewGuid(),
            Provider = "simplefin",
            AccessUrlCiphertext = "https://demo:secret@bridge.simplefin.org/simplefin",
            AccessUrlHint = "bridge.simplefin.org",
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.SimpleFinConnections.Add(connection);
        await dbContext.SaveChangesAsync();

        var category = await SeedCategoryAsync(dbContext, "Bills", "Gas");
        var autoCategorizationService = new AutoCategorizationService(dbContext);
        await autoCategorizationService.SaveRuleAsync(new SaveCategoryRuleRequest(category.Id, "GIBSONCOUNT"), CancellationToken.None);

        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "errlist": [],
              "connections": [
                {
                  "conn_id": "CON-1",
                  "name": "Checking Bank",
                  "org_id": "ORG-1",
                  "org_url": "https://bank.example",
                  "sfin_url": "https://bank.example/simplefin"
                }
              ],
              "accounts": [
                {
                  "id": "CHK-1",
                  "name": "Primary Checking",
                  "conn_id": "CON-1",
                  "currency": "USD",
                  "balance": "2500.12",
                  "available-balance": "2400.12",
                  "balance-date": 1715000000,
                  "transactions": [
                    {
                      "id": "TX-GAS",
                      "posted": 1715000000,
                      "amount": "-42.00",
                      "description": "PAYPAL INST XFER GIBSONCOUNT",
                      "payee": "GIBSONCOUNT",
                      "pending": false
                    }
                  ]
                }
              ]
            }
            """)
        }));

        var client = new SimpleFinClient(httpClient);
        var importer = new SimpleFinAccountSetImporter(dbContext);
        var service = new SimpleFinSyncService(dbContext, client, importer, autoCategorizationService);

        await service.RunSyncAsync(connection.Id, CancellationToken.None);

        var transaction = await dbContext.Transactions.SingleAsync(x => x.ProviderTransactionId == "TX-GAS");
        Assert.Equal(category.Id, transaction.CategoryId);
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

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_responseFactory(request));
    }
}
