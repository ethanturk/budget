using System.ComponentModel.DataAnnotations;

namespace BudgetApp.Application.Configuration;

public sealed class MasterPasswordAuthOptions
{
    public const string SectionName = "Auth";

    [Required]
    public string MasterPasswordHash { get; init; } = string.Empty;
}
