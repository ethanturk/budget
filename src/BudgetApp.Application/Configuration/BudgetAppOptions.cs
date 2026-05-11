using System.ComponentModel.DataAnnotations;

namespace BudgetApp.Application.Configuration;

public sealed class BudgetAppOptions
{
    public const string SectionName = "BudgetApp";

    [Required]
    [RegularExpression("^[A-Z]{3}$")]
    public string DefaultCurrency { get; init; } = "USD";

    [Range(1, 1440)]
    public int SyncIntervalMinutes { get; init; } = 15;
}
