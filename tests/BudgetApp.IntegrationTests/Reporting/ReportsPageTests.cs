using System.Net;
using BudgetApp.Web.Components;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BudgetApp.IntegrationTests.Reporting;

public sealed class ReportsPageTests : IClassFixture<WebApplicationFactory<App>>
{
    private readonly WebApplicationFactory<App> _factory;

    public ReportsPageTests(WebApplicationFactory<App> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuthenticatedReportsPage_ShowsMonthSelectorAndSpendingReport()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var authCookie = await SignInAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/reports?month=2026-05");
        request.Headers.Add("Cookie", authCookie);

        using var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Reports", html);
        Assert.Contains("Month", html);
        Assert.Contains("value=\"2026-05\"", html);
        Assert.Contains("Spending vs budget", html);
        Assert.Contains("Budgeted", html);
        Assert.Contains("Spent", html);
        Assert.Contains("Remaining", html);
        Assert.Contains("Spending status", html);
        Assert.Contains("On track", html);
        Assert.Contains("Near limit", html);
        Assert.Contains("Over budget", html);
    }

    [Fact]
    public async Task AuthenticatedNavigation_IncludesReportsLink()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var authCookie = await SignInAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Cookie", authCookie);

        using var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("href=\"reports\"", html);
        Assert.Contains("Reports", html);
    }

    private static async Task<string> SignInAsync(HttpClient client)
    {
        using var loginResponse = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["password"] = "change-me"
        }));

        return loginResponse.Headers
            .GetValues("Set-Cookie")
            .First(x => x.StartsWith(".AspNetCore.Cookies=", StringComparison.OrdinalIgnoreCase))
            .Split(';', 2)[0];
    }
}
