using BudgetApp.Application.Configuration;
using BudgetApp.Infrastructure.Budgeting;
using BudgetApp.Infrastructure.Persistence;
using BudgetApp.Infrastructure.Reporting;
using BudgetApp.Infrastructure.SimpleFin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BudgetApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("BudgetApp")
            ?? "Host=localhost;Database=budget_app;Username=postgres;Password=postgres";

        services.AddDbContext<BudgetAppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(BudgetAppDbContext).Assembly.FullName);
            });
        });

        services.AddHttpClient<SimpleFinClient>((serviceProvider, httpClient) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<BudgetAppOptions>>().Value;
            httpClient.Timeout = TimeSpan.FromSeconds(options.SimpleFinHttpTimeoutSeconds);
        });
        services.AddScoped<SimpleFinAccountSetImporter>();
        services.AddScoped<SimpleFinConnectionService>();
        services.AddScoped<SimpleFinSyncService>();
        services.AddScoped<DashboardQueryService>();
        services.AddScoped<AccountTransactionQueryService>();
        services.AddScoped<BudgetMonthSummaryService>();
        services.AddScoped<BudgetEditorService>();
        services.AddScoped<CategoryManagementService>();
        services.AddScoped<SpendingReportService>();
        services.AddScoped<TransactionCategorizationService>();
        services.AddScoped<TransactionReviewService>();
        services.AddScoped<AutoCategorizationService>();

        return services;
    }
}
