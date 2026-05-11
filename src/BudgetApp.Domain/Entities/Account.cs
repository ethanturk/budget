namespace BudgetApp.Domain.Entities;

public sealed class Account
{
    public Guid Id { get; set; }
    public Guid? ConnectionId { get; set; }
    public Guid? InstitutionId { get; set; }
    public string Provider { get; set; } = "simplefin";
    public string ProviderConnectionId { get; set; } = string.Empty;
    public string ProviderAccountId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? OfficialName { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public string AccountType { get; set; } = "other";
    public bool IsClosed { get; set; }
    public string? Last4 { get; set; }
    public int SortIndex { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public SimpleFinConnection? Connection { get; set; }
    public Institution? Institution { get; set; }
    public ICollection<AccountBalanceSnapshot> BalanceSnapshots { get; set; } = new List<AccountBalanceSnapshot>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
