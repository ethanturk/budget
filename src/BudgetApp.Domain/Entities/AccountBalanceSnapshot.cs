namespace BudgetApp.Domain.Entities;

public sealed class AccountBalanceSnapshot
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public DateTimeOffset AsOfAt { get; set; }
    public decimal? AvailableAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public decimal? LimitAmount { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public Guid? SyncRunId { get; set; }

    public Account Account { get; set; } = null!;
    public SyncRun? SyncRun { get; set; }
}
