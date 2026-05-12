using System.Globalization;

namespace BudgetApp.Infrastructure.Budgeting.Rules;

public static class CategoryRuleDefinitionValidator
{
    public static void Validate(CategoryRuleDefinition definition)
    {
        if (definition.Root is null)
        {
            throw new InvalidOperationException("Rule must include at least one condition.");
        }

        ValidateNode(definition.Root);
    }

    private static void ValidateNode(RuleNode node)
    {
        switch (node)
        {
            case RuleGroupNode group:
                if (group.Children.Count == 0)
                {
                    throw new InvalidOperationException("Rule must include at least one condition.");
                }

                foreach (var child in group.Children)
                {
                    ValidateNode(child);
                }

                break;
            case RuleConditionNode condition:
                ValidateCondition(condition);
                break;
            default:
                throw new InvalidOperationException("Rule condition is invalid.");
        }
    }

    private static void ValidateCondition(RuleConditionNode condition)
    {
        if (IsStringField(condition.Field))
        {
            if (!IsStringComparison(condition.Comparison))
            {
                throw new InvalidOperationException($"{FormatField(condition.Field)} does not support {FormatComparison(condition.Comparison)}.");
            }

            if (RequiresValue(condition.Comparison) && string.IsNullOrWhiteSpace(condition.Value))
            {
                throw new InvalidOperationException($"{FormatField(condition.Field)} {FormatComparison(condition.Comparison)} requires a value.");
            }

            return;
        }

        if (IsDecimalField(condition.Field))
        {
            if (!IsDecimalComparison(condition.Comparison))
            {
                throw new InvalidOperationException($"{FormatField(condition.Field)} does not support {FormatComparison(condition.Comparison)}.");
            }

            if (!decimal.TryParse(condition.Value, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
            {
                throw new InvalidOperationException($"{FormatField(condition.Field)} comparison requires a valid amount.");
            }

            return;
        }

        if (condition.Field == RuleField.IsPending)
        {
            if (condition.Comparison is not (RuleComparison.IsTrue or RuleComparison.IsFalse))
            {
                throw new InvalidOperationException("Pending status only supports true/false comparisons.");
            }

            return;
        }

        throw new InvalidOperationException("Rule field is invalid.");
    }

    public static bool IsStringField(RuleField field) =>
        field is RuleField.Description or RuleField.Payee or RuleField.Memo;

    public static bool IsDecimalField(RuleField field) =>
        field is RuleField.Amount or RuleField.SpendingAmount;

    public static bool IsStringComparison(RuleComparison comparison) =>
        comparison is RuleComparison.Contains
            or RuleComparison.Equals
            or RuleComparison.StartsWith
            or RuleComparison.EndsWith
            or RuleComparison.IsBlank
            or RuleComparison.IsNotBlank;

    public static bool IsDecimalComparison(RuleComparison comparison) =>
        comparison is RuleComparison.Equals
            or RuleComparison.GreaterThan
            or RuleComparison.GreaterThanOrEqual
            or RuleComparison.LessThan
            or RuleComparison.LessThanOrEqual;

    public static bool RequiresValue(RuleComparison comparison) =>
        comparison is not (RuleComparison.IsBlank or RuleComparison.IsNotBlank or RuleComparison.IsTrue or RuleComparison.IsFalse);

    private static string FormatField(RuleField field) => CategoryRuleDisplayFormatter.FormatField(field);

    private static string FormatComparison(RuleComparison comparison) => CategoryRuleDisplayFormatter.FormatComparison(comparison);
}
