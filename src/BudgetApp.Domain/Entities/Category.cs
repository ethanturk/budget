namespace BudgetApp.Domain.Entities;

public sealed class Category
{
    public Guid Id { get; set; }
    public Guid CategoryGroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortIndex { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public CategoryGroup CategoryGroup { get; set; } = null!;
    public ICollection<BudgetAllocation> BudgetAllocations { get; set; } = new List<BudgetAllocation>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<CategoryRule> CategoryRules { get; set; } = new List<CategoryRule>();
}
