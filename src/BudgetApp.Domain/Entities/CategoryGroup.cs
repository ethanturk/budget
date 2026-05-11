namespace BudgetApp.Domain.Entities;

public sealed class CategoryGroup
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortIndex { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Category> Categories { get; set; } = new List<Category>();
}
