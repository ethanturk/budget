using System.Text.Json.Serialization;

namespace BudgetApp.Infrastructure.Budgeting.Rules;

public sealed record CategoryRuleDefinition(RuleNode Root);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RuleGroupNode), "group")]
[JsonDerivedType(typeof(RuleConditionNode), "condition")]
public abstract record RuleNode;

public sealed record RuleGroupNode(
    RuleLogicalOperator Operator,
    IReadOnlyList<RuleNode> Children) : RuleNode;

public sealed record RuleConditionNode(
    RuleField Field,
    RuleComparison Comparison,
    string? Value) : RuleNode;

public enum RuleLogicalOperator
{
    And,
    Or
}

public enum RuleField
{
    Description,
    Payee,
    Memo,
    Amount,
    SpendingAmount,
    IsPending
}

public enum RuleComparison
{
    Contains,
    Equals,
    StartsWith,
    EndsWith,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    IsBlank,
    IsNotBlank,
    IsTrue,
    IsFalse
}
