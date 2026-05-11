using System.Net;
using BudgetApp.Web.Components;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BudgetApp.IntegrationTests.SimpleFin;

public sealed class SimpleFinConnectPageTests : IClassFixture<WebApplicationFactory<App>>
{
    private readonly WebApplicationFactory<App> _factory;

    public SimpleFinConnectPageTests(WebApplicationFactory<App> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetConnectPage_AsAuthenticatedUser_ShowsSetupTokenForm()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var authCookie = await SignInAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/simplefin/connect");
        request.Headers.Add("Cookie", authCookie);

        using var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("SimpleFIN setup token", html);
        Assert.Contains("Connect institution", html);
    }

    [Fact]
    public async Task PostConnect_WithInvalidSetupToken_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var authCookie = await SignInAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/simplefin/connect");
        request.Headers.Add("Cookie", authCookie);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["setupToken"] = "not-base64"
        });

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Invalid setup token", body);
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
