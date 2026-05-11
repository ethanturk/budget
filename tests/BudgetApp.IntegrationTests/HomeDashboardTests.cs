using System.Net;
using BudgetApp.Web.Components;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BudgetApp.IntegrationTests.Dashboard;

public sealed class HomeDashboardTests : IClassFixture<WebApplicationFactory<App>>
{
    private readonly WebApplicationFactory<App> _factory;

    public HomeDashboardTests(WebApplicationFactory<App> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuthenticatedHome_ShowsBudgetDashboardSections()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var loginResponse = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["password"] = "change-me"
        }));

        var authCookie = loginResponse.Headers
            .GetValues("Set-Cookie")
            .First(x => x.StartsWith(".AspNetCore.Cookies=", StringComparison.OrdinalIgnoreCase))
            .Split(';', 2)[0];

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Cookie", authCookie);

        using var homeResponse = await client.SendAsync(request);
        var html = await homeResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);
        Assert.Contains("Connected institutions", html);
        Assert.Contains("Recent transactions", html);
        Assert.Contains("Budget health", html);
        Assert.Contains("Recent sync activity", html);
    }
}
