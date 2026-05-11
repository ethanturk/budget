using BudgetApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.IntegrationTests.Persistence;

public sealed class BudgetAppDbContextSchemaTests
{
    [Fact]
    public void GenerateCreateScript_IncludesLedgerFoundationTables()
    {
        var options = new DbContextOptionsBuilder<BudgetAppDbContext>()
            .UseNpgsql("Host=localhost;Database=budget_app_test;Username=postgres;Password=postgres")
            .Options;

        using var context = new BudgetAppDbContext(options);

        var script = context.Database.GenerateCreateScript();

        Assert.Contains("simplefin_connections", script);
        Assert.Contains("sync_runs", script);
        Assert.Contains("institutions", script);
        Assert.Contains("accounts", script);
        Assert.Contains("account_balance_snapshots", script);
        Assert.Contains("transactions", script);
        Assert.Contains("category_groups", script);
        Assert.Contains("categories", script);
        Assert.Contains("budget_months", script);
        Assert.Contains("budget_allocations", script);
    }
}
