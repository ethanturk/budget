namespace BudgetApp.Domain.Entities;

public sealed class SimpleFinConnection
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = "simplefin";
    public string AccessUrlCiphertext { get; set; } = string.Empty;
    public string? AccessUrlHint { get; set; }
    public string Status { get; set; } = "active";
    public DateTimeOffset? LastSuccessfulSyncAt { get; set; }
    public DateTimeOffset? LastAttemptedSyncAt { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Account> Accounts { get; set; } = new List<Account>();
    public ICollection<SyncRun> SyncRuns { get; set; } = new List<SyncRun>();
}
