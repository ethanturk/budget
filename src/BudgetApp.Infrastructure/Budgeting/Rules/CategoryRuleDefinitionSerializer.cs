using System.Text.Json;
using System.Text.Json.Serialization;

namespace BudgetApp.Infrastructure.Budgeting.Rules;

public static class CategoryRuleDefinitionSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(CategoryRuleDefinition definition)
    {
        CategoryRuleDefinitionValidator.Validate(definition);
        return JsonSerializer.Serialize(definition, Options);
    }

    public static CategoryRuleDefinition Deserialize(string ruleJson)
    {
        if (string.IsNullOrWhiteSpace(ruleJson))
        {
            throw new InvalidOperationException("Rule definition is required.");
        }

        var definition = JsonSerializer.Deserialize<CategoryRuleDefinition>(ruleJson, Options)
            ?? throw new InvalidOperationException("Rule definition is invalid.");
        CategoryRuleDefinitionValidator.Validate(definition);
        return definition;
    }
}
