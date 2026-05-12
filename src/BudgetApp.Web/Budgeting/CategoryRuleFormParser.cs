using BudgetApp.Infrastructure.Budgeting.Rules;
using Microsoft.AspNetCore.Http;

namespace BudgetApp.Web.Budgeting;

public static class CategoryRuleFormParser
{
    public static CategoryRuleDefinition Parse(IFormCollection form)
    {
        var logicalOperator = ParseLogicalOperator(form["logicalOperator"].ToString());
        var conditions = new List<RuleNode>();

        for (var index = 1; index <= 3; index++)
        {
            var fieldValue = form[$"field{index}"].ToString().Trim();
            var comparisonValue = form[$"comparison{index}"].ToString().Trim();
            var value = form[$"value{index}"].ToString().Trim();

            if (string.IsNullOrWhiteSpace(fieldValue) && string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(fieldValue) || string.IsNullOrWhiteSpace(comparisonValue))
            {
                throw new InvalidOperationException("Each rule condition requires a field and comparison.");
            }

            if (!Enum.TryParse<RuleField>(fieldValue, ignoreCase: true, out var field))
            {
                throw new InvalidOperationException("Rule field is invalid.");
            }

            if (!Enum.TryParse<RuleComparison>(comparisonValue, ignoreCase: true, out var comparison))
            {
                throw new InvalidOperationException("Rule comparison is invalid.");
            }

            conditions.Add(new RuleConditionNode(field, comparison, value));
        }

        if (conditions.Count == 0)
        {
            throw new InvalidOperationException("At least one rule condition is required.");
        }

        var root = conditions.Count == 1
            ? conditions[0]
            : new RuleGroupNode(logicalOperator, conditions);
        var definition = new CategoryRuleDefinition(root);
        CategoryRuleDefinitionValidator.Validate(definition);
        return definition;
    }

    private static RuleLogicalOperator ParseLogicalOperator(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return RuleLogicalOperator.And;
        }

        return Enum.TryParse<RuleLogicalOperator>(value, ignoreCase: true, out var logicalOperator)
            ? logicalOperator
            : throw new InvalidOperationException("Rule logical operator is invalid.");
    }
}
