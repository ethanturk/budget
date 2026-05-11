using System.Globalization;
using System.Security.Claims;
using System.Text;
using BudgetApp.Application.Configuration;
using BudgetApp.Infrastructure;
using BudgetApp.Infrastructure.Auth;
using BudgetApp.Infrastructure.Budgeting;
using BudgetApp.Infrastructure.SimpleFin;
using BudgetApp.Web.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services
    .AddOptions<BudgetAppOptions>()
    .Bind(builder.Configuration.GetSection(BudgetAppOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<MasterPasswordAuthOptions>()
    .Bind(builder.Configuration.GetSection(MasterPasswordAuthOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<MasterPasswordHasher>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapGet("/login", (HttpContext httpContext) =>
{
    if (httpContext.User.Identity?.IsAuthenticated == true)
    {
        return Results.Redirect("/");
    }

    const string html = """
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Budget App Login</title>
  <style>
    body { font-family: sans-serif; margin: 0; background: #0f172a; color: #e2e8f0; }
    main { min-height: 100vh; display: flex; align-items: center; justify-content: center; }
    form { width: 100%; max-width: 24rem; background: #111827; padding: 2rem; border-radius: 0.75rem; box-shadow: 0 10px 30px rgba(0,0,0,0.35); }
    h1 { margin-top: 0; }
    label, input, button { display: block; width: 100%; }
    input { margin-top: 0.5rem; margin-bottom: 1rem; padding: 0.75rem; border-radius: 0.5rem; border: 1px solid #334155; background: #0f172a; color: #e2e8f0; }
    button { padding: 0.75rem; border-radius: 0.5rem; border: 0; background: #2563eb; color: white; font-weight: 600; cursor: pointer; }
    .error { color: #fca5a5; margin-bottom: 1rem; }
  </style>
</head>
<body>
  <main>
    <form method="post" action="/login">
      <h1>Budget App</h1>
      <p>Enter the master password to continue.</p>
      <label for="password">Master password</label>
      <input id="password" name="password" type="password" autocomplete="current-password" autofocus />
      <button type="submit">Sign in</button>
    </form>
  </main>
</body>
</html>
""";

    return Results.Content(html, "text/html");
}).AllowAnonymous();

app.MapPost("/login", async Task<Results<RedirectHttpResult, ContentHttpResult>> (
    HttpContext httpContext,
    IOptions<MasterPasswordAuthOptions> authOptions,
    MasterPasswordHasher hasher) =>
{
    var form = await httpContext.Request.ReadFormAsync();
    var password = form["password"].ToString();

    if (!hasher.VerifyPassword(authOptions.Value.MasterPasswordHash, password))
    {
        return TypedResults.Content("Invalid password.", "text/plain", Encoding.UTF8, StatusCodes.Status401Unauthorized);
    }

    var claims = new[] { new Claim(ClaimTypes.Name, "budget-user") };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);

    await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    return TypedResults.Redirect("/");
}).DisableAntiforgery().AllowAnonymous();

app.MapGet("/simplefin/connect", () =>
{
    const string html = """
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Connect SimpleFIN</title>
  <style>
    body { font-family: sans-serif; margin: 0; background: #0f172a; color: #e2e8f0; }
    main { min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 2rem; }
    form { width: 100%; max-width: 42rem; background: #111827; padding: 2rem; border-radius: 0.75rem; box-shadow: 0 10px 30px rgba(0,0,0,0.35); }
    h1 { margin-top: 0; }
    label, textarea, button, a { display: block; width: 100%; }
    textarea { margin-top: 0.5rem; margin-bottom: 1rem; min-height: 10rem; padding: 0.75rem; border-radius: 0.5rem; border: 1px solid #334155; background: #0f172a; color: #e2e8f0; }
    button { padding: 0.75rem; border-radius: 0.5rem; border: 0; background: #2563eb; color: white; font-weight: 600; cursor: pointer; }
    .muted { color: #94a3b8; }
    .back { margin-top: 1rem; color: #93c5fd; text-decoration: none; }
  </style>
</head>
<body>
  <main>
    <form method="post" action="/simplefin/connect">
      <h1>Connect institution</h1>
      <p class="muted">Paste the SimpleFIN setup token from your bank or bridge to save a connection for background sync.</p>
      <label for="setupToken">SimpleFIN setup token</label>
      <textarea id="setupToken" name="setupToken" autocomplete="off" spellcheck="false"></textarea>
      <button type="submit">Connect institution</button>
      <a class="back" href="/">Back to dashboard</a>
    </form>
  </main>
</body>
</html>
""";

    return Results.Content(html, "text/html");
});

app.MapPost("/simplefin/connect", async Task<Results<RedirectHttpResult, ContentHttpResult>> (
    HttpContext httpContext,
    SimpleFinConnectionService connectionService,
    CancellationToken cancellationToken) =>
{
    var form = await httpContext.Request.ReadFormAsync(cancellationToken);
    var setupToken = form["setupToken"].ToString().Trim();

    if (string.IsNullOrWhiteSpace(setupToken))
    {
        return TypedResults.Content("Setup token is required.", "text/plain", Encoding.UTF8, StatusCodes.Status400BadRequest);
    }

    try
    {
        await connectionService.ConnectAsync(setupToken, cancellationToken);
        return TypedResults.Redirect("/");
    }
    catch (FormatException)
    {
        return TypedResults.Content("Invalid setup token.", "text/plain", Encoding.UTF8, StatusCodes.Status400BadRequest);
    }
    catch (InvalidOperationException ex)
    {
        return TypedResults.Content(ex.Message, "text/plain", Encoding.UTF8, StatusCodes.Status400BadRequest);
    }
}).DisableAntiforgery();

app.MapPost("/simplefin/sync/{connectionId:guid}", async Task<Results<RedirectHttpResult, NotFound<string>, ContentHttpResult>> (
    Guid connectionId,
    SimpleFinSyncService syncService,
    CancellationToken cancellationToken) =>
{
    try
    {
        await syncService.RunSyncAsync(connectionId, cancellationToken);
        return TypedResults.Redirect("/");
    }
    catch (InvalidOperationException ex)
    {
        return TypedResults.NotFound(ex.Message);
    }
}).DisableAntiforgery();

app.MapPost("/budget/allocations", async Task<Results<RedirectHttpResult, ContentHttpResult>> (
    HttpContext httpContext,
    BudgetEditorService budgetEditorService,
    CancellationToken cancellationToken) =>
{
    var form = await httpContext.Request.ReadFormAsync(cancellationToken);
    var monthValue = form["month"].ToString().Trim();
    var groupName = form["groupName"].ToString().Trim();
    var categoryName = form["categoryName"].ToString().Trim();
    var amountValue = form["amount"].ToString().Trim();

    if (!DateOnly.TryParseExact(monthValue + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
    {
        return TypedResults.Content("Budget month is required.", "text/plain", Encoding.UTF8, StatusCodes.Status400BadRequest);
    }

    if (!decimal.TryParse(amountValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
    {
        return TypedResults.Content("Allocated amount must be a valid number.", "text/plain", Encoding.UTF8, StatusCodes.Status400BadRequest);
    }

    try
    {
        await budgetEditorService.SaveAllocationAsync(
            new SaveBudgetAllocationRequest(month, groupName, categoryName, amount),
            cancellationToken);

        return TypedResults.Redirect("/budget");
    }
    catch (InvalidOperationException ex)
    {
        return TypedResults.Content(ex.Message, "text/plain", Encoding.UTF8, StatusCodes.Status400BadRequest);
    }
}).DisableAntiforgery();

app.MapPost("/budget/categories", async Task<Results<RedirectHttpResult, ContentHttpResult>> (
    HttpContext httpContext,
    CategoryManagementService categoryManagementService,
    CancellationToken cancellationToken) =>
{
    var form = await httpContext.Request.ReadFormAsync(cancellationToken);
    var groupName = form["groupName"].ToString().Trim();
    var categoryName = form["categoryName"].ToString().Trim();

    if (string.IsNullOrWhiteSpace(groupName))
    {
        return TypedResults.Content("Category group name is required.", "text/plain", Encoding.UTF8, StatusCodes.Status400BadRequest);
    }

    if (string.IsNullOrWhiteSpace(categoryName))
    {
        return TypedResults.Content("Category name is required.", "text/plain", Encoding.UTF8, StatusCodes.Status400BadRequest);
    }

    try
    {
        await categoryManagementService.SaveCategoryAsync(
            new SaveCategoryRequest(groupName, categoryName),
            cancellationToken);

        return TypedResults.Redirect("/budget");
    }
    catch (InvalidOperationException ex)
    {
        return TypedResults.Content(ex.Message, "text/plain", Encoding.UTF8, StatusCodes.Status400BadRequest);
    }
}).DisableAntiforgery();

app.MapPost("/budget/categories/{categoryId:guid}/archive", async Task<Results<RedirectHttpResult, NotFound<string>, ContentHttpResult>> (
    Guid categoryId,
    CategoryManagementService categoryManagementService,
    CancellationToken cancellationToken) =>
{
    try
    {
        await categoryManagementService.ArchiveCategoryAsync(categoryId, cancellationToken);
        return TypedResults.Redirect("/budget");
    }
    catch (InvalidOperationException ex)
    {
        return TypedResults.NotFound(ex.Message);
    }
}).DisableAntiforgery();

app.MapPost("/budget/category-rules", async Task<Results<RedirectHttpResult, ContentHttpResult>> (
    HttpContext httpContext,
    AutoCategorizationService autoCategorizationService,
    CancellationToken cancellationToken) =>
{
    var form = await httpContext.Request.ReadFormAsync(cancellationToken);
    var categoryValue = form["categoryId"].ToString().Trim();
    var matchText = form["matchText"].ToString().Trim();

    if (!Guid.TryParse(categoryValue, out var categoryId))
    {
        return TypedResults.Content("Category is required.", "text/plain", Encoding.UTF8, StatusCodes.Status400BadRequest);
    }

    if (string.IsNullOrWhiteSpace(matchText))
    {
        return TypedResults.Content("Rule match text is required.", "text/plain", Encoding.UTF8, StatusCodes.Status400BadRequest);
    }

    try
    {
        await autoCategorizationService.SaveRuleAsync(new SaveCategoryRuleRequest(categoryId, matchText), cancellationToken);
        return TypedResults.Redirect("/budget");
    }
    catch (InvalidOperationException ex)
    {
        return TypedResults.Content(ex.Message, "text/plain", Encoding.UTF8, StatusCodes.Status400BadRequest);
    }
}).DisableAntiforgery();

app.MapPost("/budget/category-rules/apply", async (
    AutoCategorizationService autoCategorizationService,
    CancellationToken cancellationToken) =>
{
    await autoCategorizationService.ApplyRulesAsync(cancellationToken);
    return Results.Redirect("/budget");
}).DisableAntiforgery();

app.MapPost("/budget/category-rules/{ruleId:guid}/deactivate", async Task<Results<RedirectHttpResult, NotFound<string>>> (
    Guid ruleId,
    AutoCategorizationService autoCategorizationService,
    CancellationToken cancellationToken) =>
{
    try
    {
        await autoCategorizationService.DeactivateRuleAsync(ruleId, cancellationToken);
        return TypedResults.Redirect("/budget");
    }
    catch (InvalidOperationException ex)
    {
        return TypedResults.NotFound(ex.Message);
    }
}).DisableAntiforgery();

app.MapPost("/budget/transactions/{transactionId:guid}/category", async Task<Results<RedirectHttpResult, NotFound<string>, ContentHttpResult>> (
    Guid transactionId,
    HttpContext httpContext,
    TransactionCategorizationService categorizationService,
    CancellationToken cancellationToken) =>
{
    var form = await httpContext.Request.ReadFormAsync(cancellationToken);
    var categoryValue = form["categoryId"].ToString().Trim();

    if (!Guid.TryParse(categoryValue, out var categoryId))
    {
        return TypedResults.Content("Category is required.", "text/plain", Encoding.UTF8, StatusCodes.Status400BadRequest);
    }

    try
    {
        await categorizationService.CategorizeTransactionAsync(transactionId, categoryId, cancellationToken);
        return TypedResults.Redirect("/budget");
    }
    catch (InvalidOperationException ex)
    {
        return TypedResults.NotFound(ex.Message);
    }
}).DisableAntiforgery();

app.MapPost("/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).DisableAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .RequireAuthorization();

app.Run();
