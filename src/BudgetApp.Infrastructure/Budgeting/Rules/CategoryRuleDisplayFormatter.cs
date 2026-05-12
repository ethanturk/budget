using System.Globalization;

namespace BudgetApp.Infrastructure.Budgeting.Rules;

public static class CategoryRuleDisplayFormatter
{
    public static string Format(CategoryRuleDefinition definition)
    {
        CategoryRuleDefinitionValidator.Validate(definition);
        return FormatNode(definition.Root);
    }

    public static string FormatField(RuleField field) => field switch
    {
        RuleField.Description => "Description",
        RuleField.Payee => "Payee",
        RuleField.Memo => "Memo",
        RuleField.Amount => "Amount",
        RuleField.SpendingAmount => "Spending amount",
        RuleField.IsPending => "Pending status",
        _ => field.ToString()
    };

    public static string FormatComparison(RuleComparison comparison) => comparison switch
    {
        RuleComparison.Contains => "contains",
        RuleComparison.Equals => "equals",
        RuleComparison.StartsWith => "starts with",
        RuleComparison.EndsWith => "ends with",
        RuleComparison.GreaterThan => ">",
        RuleComparison.GreaterThanOrEqual => ">=",
        RuleComparison.LessThan => "<",
        RuleComparison.LessThanOrEqual => "<=",
        RuleComparison.IsBlank => "is blank",
        RuleComparison.IsNotBlank => "is not blank",
        RuleComparison.IsTrue => "is true",
        RuleComparison.IsFalse => "is false",
        _ => comparison.ToString()
    };

    private static string FormatNode(RuleNode node) => node switch
    {
        RuleConditionNode condition => FormatCondition(condition),
        RuleGroupNode group => string.Join(
            $" {group.Operator.ToString().ToUpperInvariant()} ",
            group.Children.Select(child => child is RuleGroupNode ? $"({FormatNode(child)})" : FormatNode(child))),
        _ => throw new InvalidOperationException("Rule condition is invalid.")
    };

    private static string FormatCondition(RuleConditionNode condition)
    {
        var field = FormatField(condition.Field);
        var comparison = FormatComparison(condition.Comparison);
        if (!CategoryRuleDefinitionValidator.RequiresValue(condition.Comparison))
        {
            return $"{field} {comparison}";
        }

        var value = condition.Value?.Trim() ?? string.Empty;
        if (CategoryRuleDefinitionValidator.IsDecimalField(condition.Field))
        {
            value = decimal.Parse(value, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture).ToString("0.##", CultureInfo.InvariantCulture);
        }

        return CategoryRuleDefinitionValidator.IsStringField(condition.Field)
            ? $"{field} {comparison} \"{value}\""
            : $"{field} {comparison} {value}";
    }
}
