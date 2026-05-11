namespace BudgetApp.Domain.Entities;

public sealed class BudgetMonth
{
    public Guid Id { get; set; }
    public DateOnly Month { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<BudgetAllocation> BudgetAllocations { get; set; } = new List<BudgetAllocation>();
}
