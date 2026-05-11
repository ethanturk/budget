using System.ComponentModel.DataAnnotations;
using BudgetApp.Application.Configuration;

namespace BudgetApp.UnitTests.Configuration;

public sealed class BudgetAppOptionsTests
{
    [Fact]
    public void Validate_Fails_When_DefaultCurrency_IsMissing()
    {
        var options = new BudgetAppOptions
        {
            DefaultCurrency = string.Empty,
            SyncIntervalMinutes = 15
        };

        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), validationResults, validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(BudgetAppOptions.DefaultCurrency)));
    }

    [Fact]
    public void Validate_Fails_When_SyncIntervalMinutes_IsOutOfRange()
    {
        var options = new BudgetAppOptions
        {
            DefaultCurrency = "USD",
            SyncIntervalMinutes = 0
        };

        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), validationResults, validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(BudgetAppOptions.SyncIntervalMinutes)));
    }
}
