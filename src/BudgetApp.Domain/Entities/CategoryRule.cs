namespace BudgetApp.Domain.Entities;

public sealed class CategoryRule
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public string? MatchText { get; set; }
    public string RuleJson { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Category Category { get; set; } = null!;
}
