using System.Net;
using BudgetApp.Web.Components;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BudgetApp.IntegrationTests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<App>>
{
    private readonly WebApplicationFactory<App> _factory;

    public HealthEndpointTests(WebApplicationFactory<App> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealth_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
