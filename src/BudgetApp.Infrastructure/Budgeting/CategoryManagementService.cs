using BudgetApp.Domain.Entities;
using BudgetApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.Infrastructure.Budgeting;

public sealed record SaveCategoryRequest(string GroupName, string CategoryName);

public sealed record SaveCategoryResult(
    Guid GroupId,
    string GroupName,
    Guid CategoryId,
    string CategoryName,
    bool IsArchived);

public sealed record CategoryCatalog(IReadOnlyList<CategoryCatalogGroup> Groups);

public sealed record CategoryCatalogGroup(
    Guid GroupId,
    string GroupName,
    int SortIndex,
    IReadOnlyList<CategoryCatalogItem> Categories);

public sealed record CategoryCatalogItem(
    Guid CategoryId,
    string CategoryName,
    int SortIndex);

public sealed class CategoryManagementService
{
    private readonly BudgetAppDbContext _dbContext;

    public CategoryManagementService(BudgetAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CategoryCatalog> GetCatalogAsync(CancellationToken cancellationToken)
    {
        var groups = await _dbContext.CategoryGroups
            .AsNoTracking()
            .Include(x => x.Categories)
            .OrderBy(x => x.SortIndex)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var catalogGroups = groups
            .Select(group => new CategoryCatalogGroup(
                group.Id,
                group.Name,
                group.SortIndex,
                group.Categories
                    .Where(category => !category.IsArchived)
                    .OrderBy(category => category.SortIndex)
                    .ThenBy(category => category.Name)
                    .Select(category => new CategoryCatalogItem(category.Id, category.Name, category.SortIndex))
                    .ToList()))
            .Where(group => group.Categories.Count > 0)
            .ToList();

        return new CategoryCatalog(catalogGroups);
    }

    public async Task<SaveCategoryResult> SaveCategoryAsync(
        SaveCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedGroupName = request.GroupName.Trim();
        var normalizedCategoryName = request.CategoryName.Trim();

        if (string.IsNullOrWhiteSpace(normalizedGroupName))
        {
            throw new InvalidOperationException("Category group name is required.");
        }

        if (string.IsNullOrWhiteSpace(normalizedCategoryName))
        {
            throw new InvalidOperationException("Category name is required.");
        }

        var now = DateTimeOffset.UtcNow;
        var group = await _dbContext.CategoryGroups
            .FirstOrDefaultAsync(x => x.Name == normalizedGroupName, cancellationToken);

        if (group is null)
        {
            var nextGroupSortIndex = await _dbContext.CategoryGroups
                .Select(x => (int?)x.SortIndex)
                .MaxAsync(cancellationToken) ?? 0;

            group = new CategoryGroup
            {
                Id = Guid.NewGuid(),
                Name = normalizedGroupName,
                SortIndex = nextGroupSortIndex + 1,
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.CategoryGroups.Add(group);
        }
        else
        {
            group.UpdatedAt = now;
        }

        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(
                x => x.CategoryGroupId == group.Id && x.Name == normalizedCategoryName,
                cancellationToken);

        if (category is null)
        {
            var nextCategorySortIndex = await _dbContext.Categories
                .Where(x => x.CategoryGroupId == group.Id)
                .Select(x => (int?)x.SortIndex)
                .MaxAsync(cancellationToken) ?? 0;

            category = new Category
            {
                Id = Guid.NewGuid(),
                CategoryGroupId = group.Id,
                Name = normalizedCategoryName,
                SortIndex = nextCategorySortIndex + 1,
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.Categories.Add(category);
        }
        else
        {
            category.IsArchived = false;
            category.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SaveCategoryResult(group.Id, group.Name, category.Id, category.Name, category.IsArchived);
    }

    public async Task ArchiveCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(x => x.Id == categoryId, cancellationToken);

        if (category is null)
        {
            throw new InvalidOperationException("Category was not found.");
        }

        category.IsArchived = true;
        category.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
