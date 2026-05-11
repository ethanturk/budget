using System.Net;
using BudgetApp.Web.Components;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BudgetApp.IntegrationTests.Auth;

public sealed class AuthenticationFlowTests : IClassFixture<WebApplicationFactory<App>>
{
    private readonly WebApplicationFactory<App> _factory;

    public AuthenticationFlowTests(WebApplicationFactory<App> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHome_RedirectsToLogin_WhenUnauthenticated()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/login", response.Headers.Location?.AbsolutePath);
        Assert.Equal("?ReturnUrl=%2F", response.Headers.Location?.Query);
    }

    [Fact]
    public async Task GetLogin_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
