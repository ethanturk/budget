using BudgetApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.Infrastructure.Reporting;

public sealed record DashboardConnectionSummary(
    Guid ConnectionId,
    string InstitutionName,
    string Status,
    DateTimeOffset? LastSuccessfulSyncAt,
    DateTimeOffset? LastAttemptedSyncAt,
    string? LastError,
    decimal CurrentBalance,
    int AccountCount);

public sealed record DashboardRecentTransaction(
    DateTimeOffset PostedAt,
    string Description,
    string AccountName,
    decimal Amount,
    bool IsPending);

public sealed record DashboardSyncSummary(
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int AccountsSeen,
    int TransactionsSeen,
    int TransactionsInserted,
    int TransactionsUpdated,
    string? ErrorText);

public sealed record DashboardBudgetSummary(
    DateOnly? Month,
    decimal TotalBudgeted,
    int BudgetGroupCount,
    int BudgetCategoryCount);

public sealed record DashboardViewModel(
    int ConnectionCount,
    int InstitutionCount,
    int AccountCount,
    decimal TotalCurrentBalance,
    DashboardBudgetSummary Budget,
    DashboardSyncSummary? LatestSync,
    IReadOnlyList<DashboardConnectionSummary> Connections,
    IReadOnlyList<DashboardRecentTransaction> RecentTransactions,
    IReadOnlyList<DashboardSyncSummary> RecentSyncRuns);

public sealed class DashboardQueryService
{
    private readonly BudgetAppDbContext _dbContext;

    public DashboardQueryService(BudgetAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var connectionCount = await _dbContext.SimpleFinConnections.CountAsync(cancellationToken);
        var institutionCount = await _dbContext.Institutions.CountAsync(cancellationToken);
        var accountCount = await _dbContext.Accounts.CountAsync(cancellationToken);

        var latestBalances = await _dbContext.AccountBalanceSnapshots
            .GroupBy(x => x.AccountId)
            .Select(group => group
                .OrderByDescending(x => x.AsOfAt)
                .Select(x => x.CurrentAmount)
                .First())
            .ToListAsync(cancellationToken);

        var recentSyncRuns = await _dbContext.SyncRuns
            .OrderByDescending(x => x.StartedAt)
            .Take(5)
            .Select(x => new DashboardSyncSummary(
                x.Status,
                x.StartedAt,
                x.CompletedAt,
                x.AccountsSeen,
                x.TransactionsSeen,
                x.TransactionsInserted,
                x.TransactionsUpdated,
                x.ErrorText))
            .ToListAsync(cancellationToken);

        var latestBudgetMonth = await _dbContext.BudgetMonths
            .OrderByDescending(x => x.Month)
            .Select(x => new { x.Id, x.Month })
            .FirstOrDefaultAsync(cancellationToken);

        DashboardBudgetSummary budgetSummary;

        if (latestBudgetMonth is null)
        {
            budgetSummary = new DashboardBudgetSummary(null, 0m, 0, 0);
        }
        else
        {
            var budgetAllocations = await _dbContext.BudgetAllocations
                .Where(x => x.BudgetMonthId == latestBudgetMonth.Id)
                .Select(x => new
                {
                    x.Amount,
                    CategoryId = x.CategoryId,
                    GroupId = x.Category.CategoryGroupId
                })
                .ToListAsync(cancellationToken);

            budgetSummary = new DashboardBudgetSummary(
                latestBudgetMonth.Month,
                budgetAllocations.Sum(x => x.Amount),
                budgetAllocations.Select(x => x.GroupId).Distinct().Count(),
                budgetAllocations.Select(x => x.CategoryId).Distinct().Count());
        }

        var latestSync = recentSyncRuns.FirstOrDefault();

        var connections = await _dbContext.SimpleFinConnections
            .OrderByDescending(x => x.LastSuccessfulSyncAt ?? x.LastAttemptedSyncAt ?? x.CreatedAt)
            .Select(connection => new DashboardConnectionSummary(
                connection.Id,
                _dbContext.Accounts
                    .Where(account => account.ConnectionId == connection.Id)
                    .Select(account => account.Institution != null ? account.Institution.Name : null)
                    .FirstOrDefault() ?? (connection.AccessUrlHint ?? connection.Id.ToString()),
                connection.Status,
                connection.LastSuccessfulSyncAt,
                connection.LastAttemptedSyncAt,
                connection.LastError,
                _dbContext.Accounts
                    .Where(account => account.ConnectionId == connection.Id)
                    .Select(account => account.BalanceSnapshots
                        .OrderByDescending(snapshot => snapshot.AsOfAt)
                        .Select(snapshot => (decimal?)snapshot.CurrentAmount)
                        .FirstOrDefault() ?? 0m)
                    .Sum(),
                _dbContext.Accounts.Count(account => account.ConnectionId == connection.Id)))
            .ToListAsync(cancellationToken);

        var recentTransactions = await _dbContext.Transactions
            .OrderByDescending(x => x.PostedAt)
            .ThenByDescending(x => x.ImportedAt)
            .Take(10)
            .Select(x => new DashboardRecentTransaction(
                x.PostedAt,
                x.Description,
                x.Account.Name,
                x.Amount,
                x.IsPending))
            .ToListAsync(cancellationToken);

        return new DashboardViewModel(
            connectionCount,
            institutionCount,
            accountCount,
            latestBalances.Sum(),
            budgetSummary,
            latestSync,
            connections,
            recentTransactions,
            recentSyncRuns);
    }
}
