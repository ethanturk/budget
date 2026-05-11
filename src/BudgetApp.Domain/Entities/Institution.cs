namespace BudgetApp.Domain.Entities;

public sealed class Institution
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = "simplefin";
    public string ProviderInstitutionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Account> Accounts { get; set; } = new List<Account>();
}
