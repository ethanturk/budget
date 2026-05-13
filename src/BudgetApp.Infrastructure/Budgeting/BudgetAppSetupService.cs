using BudgetApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.Infrastructure.Budgeting;

public sealed class BudgetAppSetupService
{
    private readonly BudgetAppDbContext _dbContext;
    private readonly CategoryManagementService _categoryManagementService;

    public BudgetAppSetupService(
        BudgetAppDbContext dbContext,
        CategoryManagementService categoryManagementService)
    {
        _dbContext = dbContext;
        _categoryManagementService = categoryManagementService;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_dbContext.Database.IsRelational())
        {
            await _dbContext.Database.MigrateAsync(cancellationToken);
            await SeedDefaultsRelationalAsync(cancellationToken);
            return;
        }

        foreach (var group in DefaultCategoryCatalog.Groups)
        {
            foreach (var categoryName in group.CategoryNames)
            {
                await _categoryManagementService.SaveCategoryAsync(
                    new SaveCategoryRequest(group.GroupName, categoryName),
                    cancellationToken);
            }
        }
    }

    private async Task SeedDefaultsRelationalAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        for (var groupIndex = 0; groupIndex < DefaultCategoryCatalog.Groups.Count; groupIndex++)
        {
            var group = DefaultCategoryCatalog.Groups[groupIndex];
            await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO category_groups ("Id", "Name", "SortIndex", "CreatedAt", "UpdatedAt")
                VALUES ({Guid.NewGuid()}, {group.GroupName}, {groupIndex + 1}, {now}, {now})
                ON CONFLICT ("Name") DO UPDATE
                SET "SortIndex" = EXCLUDED."SortIndex",
                    "UpdatedAt" = EXCLUDED."UpdatedAt";
                """, cancellationToken);
        }

        var groupNames = DefaultCategoryCatalog.Groups
            .Select(group => group.GroupName)
            .ToArray();

        var groupIdsByName = await _dbContext.CategoryGroups
            .Where(x => groupNames.Contains(x.Name))
            .ToDictionaryAsync(x => x.Name, x => x.Id, cancellationToken);

        foreach (var group in DefaultCategoryCatalog.Groups)
        {
            var groupId = groupIdsByName[group.GroupName];

            for (var categoryIndex = 0; categoryIndex < group.CategoryNames.Count; categoryIndex++)
            {
                var categoryName = group.CategoryNames[categoryIndex];
                await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO categories ("Id", "CategoryGroupId", "Name", "SortIndex", "IsArchived", "CreatedAt", "UpdatedAt")
                    VALUES ({Guid.NewGuid()}, {groupId}, {categoryName}, {categoryIndex + 1}, {false}, {now}, {now})
                    ON CONFLICT ("CategoryGroupId", "Name") DO UPDATE
                    SET "SortIndex" = EXCLUDED."SortIndex",
                        "IsArchived" = FALSE,
                        "UpdatedAt" = EXCLUDED."UpdatedAt";
                    """, cancellationToken);
            }
        }
    }
}
