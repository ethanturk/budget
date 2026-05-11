using BudgetApp.Domain.Entities;
using BudgetApp.Infrastructure.Persistence;

namespace BudgetApp.Infrastructure.SimpleFin;

public sealed class SimpleFinConnectionService
{
    private readonly BudgetAppDbContext _dbContext;
    private readonly SimpleFinClient _simpleFinClient;

    public SimpleFinConnectionService(BudgetAppDbContext dbContext, SimpleFinClient simpleFinClient)
    {
        _dbContext = dbContext;
        _simpleFinClient = simpleFinClient;
    }

    public async Task<Guid> ConnectAsync(string setupToken, CancellationToken cancellationToken)
    {
        var accessUrl = await _simpleFinClient.ClaimAccessUrlAsync(setupToken, cancellationToken);
        var accessUri = new Uri(accessUrl);
        var now = DateTimeOffset.UtcNow;

        var connection = new SimpleFinConnection
        {
            Id = Guid.NewGuid(),
            Provider = "simplefin",
            AccessUrlCiphertext = accessUrl,
            AccessUrlHint = accessUri.Host,
            Status = "active",
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.SimpleFinConnections.Add(connection);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return connection.Id;
    }
}
