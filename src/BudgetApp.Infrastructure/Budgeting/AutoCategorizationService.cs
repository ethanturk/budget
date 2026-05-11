using BudgetApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.Infrastructure.Budgeting;

public sealed record SaveCategoryRuleRequest(Guid CategoryId, string MatchText);

public sealed record SaveCategoryRuleResult(Guid RuleId, Guid CategoryId, string MatchText);

public sealed record AutoCategorizationResult(int CategorizedCount, IReadOnlyList<AutoCategorizationMatch> Matches);

public sealed record AutoCategorizationMatch(Guid TransactionId, Guid CategoryId, string MatchText);

public sealed record CategoryRuleListItem(
    Guid RuleId,
    Guid CategoryId,
    string GroupName,
    string CategoryName,
    string MatchText,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed class AutoCategorizationService
{
    private readonly BudgetAppDbContext _dbContext;

    public AutoCategorizationService(BudgetAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SaveCategoryRuleResult> SaveRuleAsync(
        SaveCategoryRuleRequest request,
        CancellationToken cancellationToken)
    {
        var matchText = request.MatchText.Trim();
        if (string.IsNullOrWhiteSpace(matchText))
        {
            throw new InvalidOperationException("Rule match text is required.");
        }

        var categoryExists = await _dbContext.Categories
            .AnyAsync(x => x.Id == request.CategoryId, cancellationToken);

        if (!categoryExists)
        {
            throw new InvalidOperationException("Category was not found.");
        }

        var existingRule = await _dbContext.CategoryRules
            .FirstOrDefaultAsync(x => x.CategoryId == request.CategoryId && x.MatchText == matchText, cancellationToken);

        if (existingRule is not null)
        {
            existingRule.IsActive = true;
            existingRule.UpdatedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new SaveCategoryRuleResult(existingRule.Id, existingRule.CategoryId, existingRule.MatchText);
        }

        var now = DateTimeOffset.UtcNow;
        var rule = new Domain.Entities.CategoryRule
        {
            Id = Guid.NewGuid(),
            CategoryId = request.CategoryId,
            MatchText = matchText,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.CategoryRules.Add(rule);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SaveCategoryRuleResult(rule.Id, rule.CategoryId, rule.MatchText);
    }

    public async Task<IReadOnlyList<CategoryRuleListItem>> GetRulesAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.CategoryRules
            .AsNoTracking()
            .OrderBy(x => x.Category.CategoryGroup.SortIndex)
            .ThenBy(x => x.Category.CategoryGroup.Name)
            .ThenBy(x => x.Category.SortIndex)
            .ThenBy(x => x.Category.Name)
            .ThenBy(x => x.MatchText)
            .Select(x => new CategoryRuleListItem(
                x.Id,
                x.CategoryId,
                x.Category.CategoryGroup.Name,
                x.Category.Name,
                x.MatchText,
                x.IsActive,
                x.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task DeactivateRuleAsync(Guid ruleId, CancellationToken cancellationToken)
    {
        var rule = await _dbContext.CategoryRules
            .FirstOrDefaultAsync(x => x.Id == ruleId, cancellationToken);

        if (rule is null)
        {
            throw new InvalidOperationException("Auto-categorization rule was not found.");
        }

        rule.IsActive = false;
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AutoCategorizationResult> ApplyRulesAsync(CancellationToken cancellationToken)
    {
        var rules = await _dbContext.CategoryRules
            .Where(x => x.IsActive && !x.Category.IsArchived)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new { x.CategoryId, x.MatchText })
            .ToListAsync(cancellationToken);

        if (rules.Count == 0)
        {
            return new AutoCategorizationResult(0, []);
        }

        var transactions = await _dbContext.Transactions
            .Where(x => x.CategoryId == null && !x.IsPending)
            .OrderByDescending(x => x.PostedAt)
            .ToListAsync(cancellationToken);

        var matches = new List<AutoCategorizationMatch>();
        foreach (var transaction in transactions)
        {
            var rule = rules.FirstOrDefault(x =>
                transaction.Description.Contains(x.MatchText, StringComparison.OrdinalIgnoreCase));

            if (rule is null)
            {
                continue;
            }

            transaction.CategoryId = rule.CategoryId;
            transaction.UpdatedAt = DateTimeOffset.UtcNow;
            matches.Add(new AutoCategorizationMatch(transaction.Id, rule.CategoryId, rule.MatchText));
        }

        if (matches.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new AutoCategorizationResult(matches.Count, matches);
    }
}
