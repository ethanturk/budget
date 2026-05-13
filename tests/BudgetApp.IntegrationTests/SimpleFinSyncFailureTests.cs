using BudgetApp.Domain.Entities;
using BudgetApp.Infrastructure.Budgeting;
using BudgetApp.Infrastructure.Persistence;
using BudgetApp.Infrastructure.SimpleFin;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.IntegrationTests.SimpleFin;

public sealed class SimpleFinSyncFailureTests
{
    [Fact]
    public async Task RunSyncAsync_WhenClientFails_RecordsFailedRunAndLastError()
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

        using var httpClient = new HttpClient(new ThrowingHttpMessageHandler());
        var client = new SimpleFinClient(httpClient);
        var importer = new SimpleFinAccountSetImporter(dbContext);
        var autoCategorizationService = new AutoCategorizationService(dbContext);
        var service = new SimpleFinSyncService(dbContext, client, importer, autoCategorizationService);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => service.RunSyncAsync(connection.Id, CancellationToken.None));

        Assert.Contains("network unavailable", exception.Message);

        var run = await dbContext.SyncRuns.SingleAsync();
        Assert.Equal("failed", run.Status);
        Assert.NotNull(run.CompletedAt);
        Assert.Contains("network unavailable", run.ErrorText);

        var reloadedConnection = await dbContext.SimpleFinConnections.SingleAsync();
        Assert.Contains("network unavailable", reloadedConnection.LastError);
        Assert.Null(reloadedConnection.LastSuccessfulSyncAt);
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("network unavailable");
    }
}
