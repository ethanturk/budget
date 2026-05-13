using System.Net;
using BudgetApp.Domain.Entities;
using BudgetApp.Infrastructure.Budgeting.Rules;
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
        Assert.Contains("name=\"selectedGroupName\"", html);
        Assert.Contains("Transportation", html);
        Assert.Contains("Or add a new category group", html);
        Assert.Contains("Filter transactions", html);
        Assert.Contains("Description search", html);
        Assert.Contains("Minimum amount", html);
        Assert.Contains("Apply filters", html);
        Assert.Contains("Bulk assign filtered transactions", html);
        Assert.Contains("Assign all visible", html);
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
    public async Task PostCategory_WithNewGroupName_CreatesNewGroupAndCategory()
    {
        await using var factory = CreateFactoryWithInMemoryDatabase();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var authCookie = await SignInAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/budget/categories");
        request.Headers.Add("Cookie", authCookie);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["selectedGroupName"] = "Transportation",
            ["newGroupName"] = "Housing",
            ["categoryName"] = "Rent"
        });

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BudgetAppDbContext>();
        var createdCategory = await dbContext.Categories
            .Include(x => x.CategoryGroup)
            .SingleAsync(x => x.Name == "Rent");

        Assert.Equal("Housing", createdCategory.CategoryGroup.Name);
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
    public async Task PostAutoCategorizationRule_WithMissingCondition_ReturnsBadRequest()
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
            ["logicalOperator"] = "And",
            ["field1"] = "",
            ["comparison1"] = "Contains",
            ["value1"] = ""
        });

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("At least one rule condition is required.", body);
    }

    [Fact]
    public async Task PostAutoCategorizationRule_WithExpressionForm_CreatesRule()
    {
        await using var factory = CreateFactoryWithInMemoryDatabase();
        var categoryId = await SeedCategoryAsync(factory);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var authCookie = await SignInAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/budget/category-rules");
        request.Headers.Add("Cookie", authCookie);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["categoryId"] = categoryId.ToString(),
            ["logicalOperator"] = "And",
            ["field1"] = "Payee",
            ["comparison1"] = "Equals",
            ["value1"] = "Interest Charge",
            ["field2"] = "Amount",
            ["comparison2"] = "LessThan",
            ["value2"] = "0"
        });

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BudgetAppDbContext>();
        var rule = await dbContext.CategoryRules.SingleAsync();
        Assert.Equal("Payee equals \"Interest Charge\" AND Amount < 0", rule.DisplayText);
        Assert.Contains("Interest Charge", rule.RuleJson);
        var definition = CategoryRuleDefinitionSerializer.Deserialize(rule.RuleJson);
        Assert.Equal(rule.DisplayText, CategoryRuleDisplayFormatter.Format(definition));
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

    [Fact]
    public async Task PostBulkTransactionCategory_WithInvalidCategory_ReturnsBadRequest()
    {
        using var client = CreateClientWithInMemoryDatabase();

        var authCookie = await SignInAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/budget/transactions/bulk-category");
        request.Headers.Add("Cookie", authCookie);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["categoryId"] = "not-a-guid",
            ["transactionSearch"] = "kroger",
            ["minimumAmount"] = "50"
        });

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Category is required.", body);
    }

    private WebApplicationFactory<App> CreateFactoryWithInMemoryDatabase()
    {
        var databaseName = Guid.NewGuid().ToString();
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<BudgetAppDbContext>>();
                services.AddDbContext<BudgetAppDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
            });
        });
    }

    private HttpClient CreateClientWithInMemoryDatabase()
    {
        var factory = CreateFactoryWithInMemoryDatabase();

        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    private static async Task<Guid> SeedCategoryAsync(WebApplicationFactory<App> factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BudgetAppDbContext>();
        var group = new CategoryGroup
        {
            Id = Guid.NewGuid(),
            Name = "Debt",
            SortIndex = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var category = new Category
        {
            Id = Guid.NewGuid(),
            CategoryGroupId = group.Id,
            Name = "Interest",
            SortIndex = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CategoryGroups.Add(group);
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync();
        return category.Id;
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
