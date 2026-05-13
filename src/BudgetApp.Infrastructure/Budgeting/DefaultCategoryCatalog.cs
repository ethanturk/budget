namespace BudgetApp.Infrastructure.Budgeting;

public static class DefaultCategoryCatalog
{
    public static IReadOnlyList<DefaultCategoryGroupDefinition> Groups { get; } =
    [
        new(
            "Transportation",
            [
                "Auto Loan",
                "Gas",
                "Miscellaneous"
            ]),
        new(
            "Food",
            [
                "Fast Food",
                "Groceries",
                "Restaurants"
            ]),
        new(
            "Tech",
            [
                "AI",
                "Hardware",
                "Subscriptions",
                "Miscellaneous"
            ]),
        new(
            "Shopping",
            [
                "Miscellaneous",
                "Gifts"
            ])
    ];
}

public sealed record DefaultCategoryGroupDefinition(
    string GroupName,
    IReadOnlyList<string> CategoryNames);
