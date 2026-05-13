using BudgetApp.Domain.Entities;
using BudgetApp.Infrastructure.Budgeting;
using BudgetApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.Infrastructure.SimpleFin;

public sealed record SimpleFinSyncResult(
    Guid SyncRunId,
    string Status,
    int AccountsSeen,
    int TransactionsSeen,
    int TransactionsInserted,
    int TransactionsUpdated);

public sealed class SimpleFinSyncService
{
    private readonly BudgetAppDbContext _dbContext;
    private readonly SimpleFinClient _simpleFinClient;
    private readonly SimpleFinAccountSetImporter _importer;
    private readonly AutoCategorizationService _autoCategorizationService;

    public SimpleFinSyncService(
        BudgetAppDbContext dbContext,
        SimpleFinClient simpleFinClient,
        SimpleFinAccountSetImporter importer,
        AutoCategorizationService autoCategorizationService)
    {
        _dbContext = dbContext;
        _simpleFinClient = simpleFinClient;
        _importer = importer;
        _autoCategorizationService = autoCategorizationService;
    }

    public async Task<SimpleFinSyncResult> RunSyncAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var connection = await _dbContext.SimpleFinConnections
            .SingleOrDefaultAsync(x => x.Id == connectionId, cancellationToken)
            ?? throw new InvalidOperationException($"SimpleFIN connection '{connectionId}' was not found.");

        var startedAt = DateTimeOffset.UtcNow;
        connection.LastAttemptedSyncAt = startedAt;
        connection.UpdatedAt = startedAt;

        var syncRun = new SyncRun
        {
            Id = Guid.NewGuid(),
            ConnectionId = connection.Id,
            TriggerSource = "manual",
            Status = "running",
            StartedAt = startedAt
        };

        _dbContext.SyncRuns.Add(syncRun);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var payload = await _simpleFinClient.GetAccountsAsync(connection.AccessUrlCiphertext, cancellationToken);
            var summary = await _importer.ImportAsync(connection.Id, payload, cancellationToken);
            await _autoCategorizationService.ApplyRulesAsync(cancellationToken);

            syncRun.Status = "succeeded";
            syncRun.CompletedAt = DateTimeOffset.UtcNow;
            syncRun.AccountsSeen = summary.AccountsSeen;
            syncRun.TransactionsSeen = summary.TransactionsSeen;
            syncRun.TransactionsInserted = summary.TransactionsInserted;
            syncRun.TransactionsUpdated = summary.TransactionsUpdated;

            connection.LastSuccessfulSyncAt = syncRun.CompletedAt;
            connection.LastError = null;
            connection.UpdatedAt = syncRun.CompletedAt.Value;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return new SimpleFinSyncResult(
                syncRun.Id,
                syncRun.Status,
                syncRun.AccountsSeen,
                syncRun.TransactionsSeen,
                syncRun.TransactionsInserted,
                syncRun.TransactionsUpdated);
        }
        catch (Exception ex)
        {
            syncRun.Status = "failed";
            syncRun.CompletedAt = DateTimeOffset.UtcNow;
            syncRun.ErrorText = ex.Message;

            connection.LastError = ex.Message;
            connection.UpdatedAt = syncRun.CompletedAt.Value;

            await _dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }
}
