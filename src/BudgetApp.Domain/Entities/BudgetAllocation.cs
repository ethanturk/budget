namespace BudgetApp.Domain.Entities;

public sealed class BudgetAllocation
{
    public Guid Id { get; set; }
    public Guid BudgetMonthId { get; set; }
    public Guid CategoryId { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public BudgetMonth BudgetMonth { get; set; } = null!;
    public Category Category { get; set; } = null!;
}
