using System.Reflection;
using BudgetApp.Infrastructure.Budgeting;
using BudgetApp.Infrastructure.Persistence;
using BudgetApp.Infrastructure.Reporting;
using BudgetApp.Infrastructure.SimpleFin;
using BudgetApp.Web.Components;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BudgetApp.IntegrationTests.Persistence;

public sealed class DependencyInjectionTests : IClassFixture<WebApplicationFactory<App>>
{
    private readonly WebApplicationFactory<App> _factory;

    public DependencyInjectionTests(WebApplicationFactory<App> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void AppServices_ResolveBudgetAppDbContext()
    {
        using var scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<BudgetAppDbContext>();

        Assert.NotNull(dbContext);
    }

    [Fact]
    public void AppServices_ConfigureSimpleFinHttpClientWithExtendedTimeout()
    {
        using var scope = _factory.Services.CreateScope();

        var client = scope.ServiceProvider.GetRequiredService<SimpleFinClient>();
        var httpClientField = typeof(SimpleFinClient).GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SimpleFIN client HttpClient field was not found.");
        var httpClient = Assert.IsType<HttpClient>(httpClientField.GetValue(client));

        Assert.Equal(TimeSpan.FromMinutes(10), httpClient.Timeout);
    }

    [Fact]
    public void AppServices_ResolveSimpleFinServices()
    {
        using var scope = _factory.Services.CreateScope();

        var client = scope.ServiceProvider.GetRequiredService<SimpleFinClient>();
        var importer = scope.ServiceProvider.GetRequiredService<SimpleFinAccountSetImporter>();
        var connectionService = scope.ServiceProvider.GetRequiredService<SimpleFinConnectionService>();
        var syncService = scope.ServiceProvider.GetRequiredService<SimpleFinSyncService>();
        var dashboardQueryService = scope.ServiceProvider.GetRequiredService<DashboardQueryService>();
        var budgetMonthSummaryService = scope.ServiceProvider.GetRequiredService<BudgetMonthSummaryService>();
        var budgetEditorService = scope.ServiceProvider.GetRequiredService<BudgetEditorService>();

        Assert.NotNull(client);
        Assert.NotNull(importer);
        Assert.NotNull(connectionService);
        Assert.NotNull(syncService);
        Assert.NotNull(dashboardQueryService);
        Assert.NotNull(budgetMonthSummaryService);
        Assert.NotNull(budgetEditorService);
    }
}
