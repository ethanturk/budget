using System.Net;
using BudgetApp.Infrastructure.Persistence;
using BudgetApp.Web.Components;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BudgetApp.IntegrationTests.Budgeting;

public sealed class BudgetPageTests : IClassFixture<WebApplicationFactory<App>>
{
    private readonly WebApplicationFactory<App> _factory;

    public BudgetPageTests(WebApplicationFactory<App> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuthenticatedBudgetPage_ShowsBudgetWorkspaceSections()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var authCookie = await SignInAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/budget");
        request.Headers.Add("Cookie", authCookie);

        using var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Budget plan", html);
        Assert.Contains("Create or update allocation", html);
        Assert.Contains("Current month summary", html);
        Assert.Contains("Manage categories", html);
        Assert.Contains("Category catalog", html);
        Assert.Contains("Spending vs budget", html);
        Assert.Contains("Review uncategorized transactions", html);
        Assert.Contains("Filter transactions", html);
        Assert.Contains("Description search", html);
        Assert.Contains("Minimum amount", html);
        Assert.Contains("Apply filters", html);
        Assert.Contains("Auto-categorization rules", html);
        Assert.Contains("Existing rules", html);
        Assert.Contains("Disable rule", html);
    }

    [Fact]
    public async Task PostAllocation_WithInvalidMonth_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var authCookie = await SignInAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/budget/allocations");
        request.Headers.Add("Cookie", authCookie);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["month"] = "not-a-month",
            ["groupName"] = "Housing",
            ["categoryName"] = "Rent",
            ["amount"] = "1250.00"
        });

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Budget month is required.", body);
    }

    [Fact]
    public async Task PostCategory_WithMissingName_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var authCookie = await SignInAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/budget/categories");
        request.Headers.Add("Cookie", authCookie);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["groupName"] = "Housing",
            ["categoryName"] = ""
        });

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Category name is required.", body);
    }

    [Fact]
    public async Task PostTransactionCategory_WithInvalidCategory_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var authCookie = await SignInAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/budget/transactions/{Guid.NewGuid()}/category");
        request.Headers.Add("Cookie", authCookie);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["categoryId"] = "not-a-guid"
        });

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Category is required.", body);
    }

    [Fact]
    public async Task PostAutoCategorizationRule_WithMissingMatchText_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var authCookie = await SignInAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/budget/category-rules");
        request.Headers.Add("Cookie", authCookie);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["categoryId"] = Guid.NewGuid().ToString(),
            ["matchText"] = ""
        });

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Rule match text is required.", body);
    }

    [Fact]
    public async Task PostCategoryRuleDeactivate_WithUnknownRule_ReturnsNotFound()
    {
        using var client = CreateClientWithInMemoryDatabase();

        var authCookie = await SignInAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/budget/category-rules/{Guid.NewGuid()}/deactivate");
        request.Headers.Add("Cookie", authCookie);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Auto-categorization rule was not found.", body);
    }

    private HttpClient CreateClientWithInMemoryDatabase()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<BudgetAppDbContext>>();
                services.AddDbContext<BudgetAppDbContext>(options =>
                    options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            });
        });

        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
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
