using System.Globalization;
using System.Linq.Expressions;
using BudgetApp.Domain.Entities;

namespace BudgetApp.Infrastructure.Budgeting.Rules;

public sealed class CategoryRuleExpressionCompiler
{
    private static readonly Expression FalseExpression = Expression.Constant(false);

    public Expression<Func<Transaction, bool>> BuildExpression(CategoryRuleDefinition definition)
    {
        CategoryRuleDefinitionValidator.Validate(definition);
        var transaction = Expression.Parameter(typeof(Transaction), "transaction");
        var body = BuildNode(definition.Root, transaction);
        return Expression.Lambda<Func<Transaction, bool>>(body, transaction);
    }

    public Func<Transaction, bool> Compile(CategoryRuleDefinition definition) =>
        BuildExpression(definition).Compile();

    private static Expression BuildNode(RuleNode node, ParameterExpression transaction) => node switch
    {
        RuleGroupNode group => BuildGroup(group, transaction),
        RuleConditionNode condition => BuildCondition(condition, transaction),
        _ => throw new InvalidOperationException("Rule condition is invalid.")
    };

    private static Expression BuildGroup(RuleGroupNode group, ParameterExpression transaction)
    {
        var childExpressions = group.Children.Select(child => BuildNode(child, transaction)).ToList();
        if (childExpressions.Count == 0)
        {
            return FalseExpression;
        }

        return group.Operator == RuleLogicalOperator.And
            ? childExpressions.Aggregate(Expression.AndAlso)
            : childExpressions.Aggregate(Expression.OrElse);
    }

    private static Expression BuildCondition(RuleConditionNode condition, ParameterExpression transaction)
    {
        if (CategoryRuleDefinitionValidator.IsStringField(condition.Field))
        {
            return BuildStringCondition(condition, transaction);
        }

        if (CategoryRuleDefinitionValidator.IsDecimalField(condition.Field))
        {
            return BuildDecimalCondition(condition, transaction);
        }

        if (condition.Field == RuleField.IsPending)
        {
            var property = Expression.Property(transaction, nameof(Transaction.IsPending));
            return condition.Comparison == RuleComparison.IsTrue
                ? property
                : Expression.Not(property);
        }

        throw new InvalidOperationException("Rule field is invalid.");
    }

    private static Expression BuildStringCondition(RuleConditionNode condition, ParameterExpression transaction)
    {
        var property = condition.Field switch
        {
            RuleField.Description => Expression.Property(transaction, nameof(Transaction.Description)),
            RuleField.Payee => Expression.Property(transaction, nameof(Transaction.Payee)),
            RuleField.Memo => Expression.Property(transaction, nameof(Transaction.Memo)),
            _ => throw new InvalidOperationException("Rule field is invalid.")
        };

        var method = typeof(CategoryRuleExpressionCompiler).GetMethod(nameof(MatchString), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?? throw new InvalidOperationException("String matcher was not found.");
        return Expression.Call(
            method,
            property,
            Expression.Constant(condition.Comparison),
            Expression.Constant(condition.Value));
    }

    private static Expression BuildDecimalCondition(RuleConditionNode condition, ParameterExpression transaction)
    {
        var property = Expression.Property(transaction, nameof(Transaction.Amount));
        Expression left = condition.Field == RuleField.SpendingAmount
            ? Expression.Negate(property)
            : property;
        var amount = decimal.Parse(condition.Value ?? string.Empty, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var right = Expression.Constant(amount);

        return condition.Comparison switch
        {
            RuleComparison.Equals => Expression.Equal(left, right),
            RuleComparison.GreaterThan => Expression.GreaterThan(left, right),
            RuleComparison.GreaterThanOrEqual => Expression.GreaterThanOrEqual(left, right),
            RuleComparison.LessThan => Expression.LessThan(left, right),
            RuleComparison.LessThanOrEqual => Expression.LessThanOrEqual(left, right),
            _ => throw new InvalidOperationException("Amount comparison is invalid.")
        };
    }

    private static bool MatchString(string? actual, RuleComparison comparison, string? expected)
    {
        var value = actual?.Trim();
        if (comparison == RuleComparison.IsBlank)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        if (comparison == RuleComparison.IsNotBlank)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var needle = expected.Trim();
        return comparison switch
        {
            RuleComparison.Contains => value.Contains(needle, StringComparison.OrdinalIgnoreCase),
            RuleComparison.Equals => value.Equals(needle, StringComparison.OrdinalIgnoreCase),
            RuleComparison.StartsWith => value.StartsWith(needle, StringComparison.OrdinalIgnoreCase),
            RuleComparison.EndsWith => value.EndsWith(needle, StringComparison.OrdinalIgnoreCase),
            _ => throw new InvalidOperationException("String comparison is invalid.")
        };
    }
}
