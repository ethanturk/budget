namespace BudgetApp.Domain.Entities;

public sealed class Transaction
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string Provider { get; set; } = "simplefin";
    public string ProviderConnectionId { get; set; } = string.Empty;
    public string ProviderTransactionId { get; set; } = string.Empty;
    public DateTimeOffset PostedAt { get; set; }
    public DateTimeOffset? TransactedAt { get; set; }
    public decimal Amount { get; set; }
    public Guid? CategoryId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Payee { get; set; }
    public string? Memo { get; set; }
    public bool IsPending { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Account Account { get; set; } = null!;
    public Category? Category { get; set; }
}
