namespace BudgetApp.Domain.Entities;

public sealed class SyncRun
{
    public Guid Id { get; set; }
    public Guid ConnectionId { get; set; }
    public string TriggerSource { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int AccountsSeen { get; set; }
    public int TransactionsSeen { get; set; }
    public int TransactionsInserted { get; set; }
    public int TransactionsUpdated { get; set; }
    public string? ErrorText { get; set; }
    public string? LockOwner { get; set; }

    public SimpleFinConnection Connection { get; set; } = null!;
}
