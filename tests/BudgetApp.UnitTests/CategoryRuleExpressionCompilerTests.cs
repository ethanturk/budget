using BudgetApp.Domain.Entities;
using BudgetApp.Infrastructure.Budgeting.Rules;

namespace BudgetApp.UnitTests;

public sealed class CategoryRuleExpressionCompilerTests
{
    [Fact]
    public void Compile_MatchesStringConditionsCaseInsensitively()
    {
        var compiler = new CategoryRuleExpressionCompiler();
        var predicate = compiler.Compile(new CategoryRuleDefinition(
            new RuleConditionNode(RuleField.Payee, RuleComparison.Contains, "kroger")));

        Assert.True(predicate(BuildTransaction(payee: "Kroger Fuel Center")));
        Assert.False(predicate(BuildTransaction(payee: "Coffee Shop")));
    }

    [Fact]
    public void Compile_MatchesDecimalComparisons()
    {
        var compiler = new CategoryRuleExpressionCompiler();
        var predicate = compiler.Compile(new CategoryRuleDefinition(
            new RuleConditionNode(RuleField.Amount, RuleComparison.LessThan, "0")));

        Assert.True(predicate(BuildTransaction(amount: -10.50m)));
        Assert.False(predicate(BuildTransaction(amount: 10.50m)));
    }

    [Fact]
    public void Compile_MatchesGroupedConditions()
    {
        var compiler = new CategoryRuleExpressionCompiler();
        var predicate = compiler.Compile(new CategoryRuleDefinition(new RuleGroupNode(
            RuleLogicalOperator.And,
            [
                new RuleConditionNode(RuleField.Payee, RuleComparison.Equals, "Interest Charge"),
                new RuleConditionNode(RuleField.Amount, RuleComparison.LessThan, "0")
            ])));

        Assert.True(predicate(BuildTransaction(payee: "Interest Charge", amount: -701.91m)));
        Assert.False(predicate(BuildTransaction(payee: "Interest Charge", amount: 12.34m)));
    }

    [Fact]
    public void Serialize_RoundTripsDefinition()
    {
        var definition = new CategoryRuleDefinition(new RuleConditionNode(RuleField.Memo, RuleComparison.Contains, "invoice"));

        var json = CategoryRuleDefinitionSerializer.Serialize(definition);
        var roundTripped = CategoryRuleDefinitionSerializer.Deserialize(json);
        var predicate = new CategoryRuleExpressionCompiler().Compile(roundTripped);

        Assert.True(predicate(BuildTransaction(memo: "Invoice 1234")));
    }

    private static Transaction BuildTransaction(string description = "Description", string? payee = null, string? memo = null, decimal amount = -1m) => new()
    {
        Id = Guid.NewGuid(),
        AccountId = Guid.NewGuid(),
        Provider = "simplefin",
        ProviderConnectionId = "conn-1",
        ProviderTransactionId = Guid.NewGuid().ToString("N"),
        PostedAt = DateTimeOffset.UtcNow,
        Amount = amount,
        Description = description,
        Payee = payee,
        Memo = memo,
        ImportedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };
}
