namespace BudgetApp.Infrastructure.Budgeting.Rules;

public static class CategoryRuleDefinitionFactory
{
    public static CategoryRuleDefinition LegacyContains(string matchText)
    {
        var trimmed = matchText.Trim();
        return new CategoryRuleDefinition(new RuleGroupNode(
            RuleLogicalOperator.Or,
            [
                new RuleConditionNode(RuleField.Description, RuleComparison.Contains, trimmed),
                new RuleConditionNode(RuleField.Payee, RuleComparison.Contains, trimmed),
                new RuleConditionNode(RuleField.Memo, RuleComparison.Contains, trimmed)
            ]));
    }
}
