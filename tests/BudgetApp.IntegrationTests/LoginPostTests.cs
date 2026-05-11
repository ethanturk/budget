using System.Net;
using BudgetApp.Web.Components;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BudgetApp.IntegrationTests.Auth;

public sealed class LoginPostTests : IClassFixture<WebApplicationFactory<App>>
{
    private readonly WebApplicationFactory<App> _factory;

    public LoginPostTests(WebApplicationFactory<App> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostLogin_ReturnsUnauthorized_ForInvalidPassword()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["password"] = "wrong-password"
        }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostLogin_SetsAuthCookie_AndAllowsAccessToHome()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var loginResponse = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["password"] = "change-me"
        }));

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        Assert.Contains(loginResponse.Headers, header => header.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase));

        var authCookie = loginResponse.Headers
            .GetValues("Set-Cookie")
            .First(x => x.StartsWith(".AspNetCore.Cookies=", StringComparison.OrdinalIgnoreCase))
            .Split(';', 2)[0];

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Cookie", authCookie);

        using var homeResponse = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);
    }
}
