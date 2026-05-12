using BudgetApp.Domain.Entities;
using BudgetApp.Infrastructure.Budgeting.Rules;
using BudgetApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.Infrastructure.Budgeting;

public sealed record SaveCategoryRuleResult(Guid RuleId, Guid CategoryId, string MatchText, string DisplayText);

public sealed record AutoCategorizationResult(int CategorizedCount, IReadOnlyList<AutoCategorizationMatch> Matches);

public sealed record AutoCategorizationMatch(Guid TransactionId, Guid CategoryId, string MatchText);

public sealed record CategoryRuleListItem(
    Guid RuleId,
    Guid CategoryId,
    string GroupName,
    string CategoryName,
    string MatchText,
    string DisplayText,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed class SaveCategoryRuleRequest
{
    public SaveCategoryRuleRequest(Guid categoryId, string matchText)
        : this(categoryId, CategoryRuleDefinitionFactory.LegacyContains(matchText.Trim()), matchText.Trim())
    {
    }

    public SaveCategoryRuleRequest(Guid categoryId, CategoryRuleDefinition definition)
        : this(categoryId, definition, null)
    {
    }

    private SaveCategoryRuleRequest(Guid categoryId, CategoryRuleDefinition definition, string? legacyMatchText)
    {
        CategoryId = categoryId;
        Definition = definition;
        LegacyMatchText = legacyMatchText;
    }

    public Guid CategoryId { get; }
    public CategoryRuleDefinition Definition { get; }
    public string? LegacyMatchText { get; }
}

public sealed class AutoCategorizationService
{
    private readonly BudgetAppDbContext _dbContext;
    private readonly CategoryRuleExpressionCompiler _expressionCompiler = new();

    public AutoCategorizationService(BudgetAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SaveCategoryRuleResult> SaveRuleAsync(
        SaveCategoryRuleRequest request,
        CancellationToken cancellationToken)
    {
        if (request.LegacyMatchText is not null && string.IsNullOrWhiteSpace(request.LegacyMatchText))
        {
            throw new InvalidOperationException("Rule match text is required.");
        }

        CategoryRuleDefinitionValidator.Validate(request.Definition);

        var categoryExists = await _dbContext.Categories
            .AnyAsync(x => x.Id == request.CategoryId, cancellationToken);

        if (!categoryExists)
        {
            throw new InvalidOperationException("Category was not found.");
        }

        var ruleJson = CategoryRuleDefinitionSerializer.Serialize(request.Definition);
        var displayText = CategoryRuleDisplayFormatter.Format(request.Definition);
        var existingRule = await _dbContext.CategoryRules
            .FirstOrDefaultAsync(x =>
                x.CategoryId == request.CategoryId
                && (x.RuleJson == ruleJson || (request.LegacyMatchText != null && x.MatchText == request.LegacyMatchText)),
                cancellationToken);

        if (existingRule is not null)
        {
            existingRule.MatchText = request.LegacyMatchText;
            existingRule.RuleJson = ruleJson;
            existingRule.DisplayText = displayText;
            existingRule.IsActive = true;
            existingRule.UpdatedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new SaveCategoryRuleResult(
                existingRule.Id,
                existingRule.CategoryId,
                request.LegacyMatchText ?? displayText,
                displayText);
        }

        var now = DateTimeOffset.UtcNow;
        var rule = new CategoryRule
        {
            Id = Guid.NewGuid(),
            CategoryId = request.CategoryId,
            MatchText = request.LegacyMatchText,
            RuleJson = ruleJson,
            DisplayText = displayText,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.CategoryRules.Add(rule);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SaveCategoryRuleResult(rule.Id, rule.CategoryId, request.LegacyMatchText ?? displayText, displayText);
    }

    public async Task<IReadOnlyList<CategoryRuleListItem>> GetRulesAsync(CancellationToken cancellationToken)
    {
        var rules = await _dbContext.CategoryRules
            .AsNoTracking()
            .OrderBy(x => x.Category.CategoryGroup.SortIndex)
            .ThenBy(x => x.Category.CategoryGroup.Name)
            .ThenBy(x => x.Category.SortIndex)
            .ThenBy(x => x.Category.Name)
            .ThenBy(x => x.DisplayText)
            .ThenBy(x => x.MatchText)
            .Select(x => new
            {
                x.Id,
                x.CategoryId,
                GroupName = x.Category.CategoryGroup.Name,
                CategoryName = x.Category.Name,
                x.MatchText,
                x.RuleJson,
                x.DisplayText,
                x.IsActive,
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return rules
            .Select(x =>
            {
                var displayText = GetDisplayText(x.RuleJson, x.DisplayText, x.MatchText);
                return new CategoryRuleListItem(
                    x.Id,
                    x.CategoryId,
                    x.GroupName,
                    x.CategoryName,
                    x.MatchText ?? displayText,
                    displayText,
                    x.IsActive,
                    x.CreatedAt);
            })
            .ToList();
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
        var ruleRows = await _dbContext.CategoryRules
            .Where(x => x.IsActive && !x.Category.IsArchived)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new { x.CategoryId, x.MatchText, x.RuleJson, x.DisplayText })
            .ToListAsync(cancellationToken);

        if (ruleRows.Count == 0)
        {
            return new AutoCategorizationResult(0, []);
        }

        var rules = ruleRows
            .Select(x =>
            {
                var definition = GetDefinition(x.RuleJson, x.MatchText);
                var displayText = GetDisplayText(x.RuleJson, x.DisplayText, x.MatchText);
                return new CompiledCategoryRule(
                    x.CategoryId,
                    x.MatchText ?? displayText,
                    _expressionCompiler.Compile(definition));
            })
            .ToList();

        var transactions = await _dbContext.Transactions
            .Where(x => x.CategoryId == null && !x.IsPending)
            .OrderByDescending(x => x.PostedAt)
            .ToListAsync(cancellationToken);

        var matches = new List<AutoCategorizationMatch>();
        foreach (var transaction in transactions)
        {
            var rule = rules.FirstOrDefault(x => x.Predicate(transaction));

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

    private static CategoryRuleDefinition GetDefinition(string? ruleJson, string? matchText)
    {
        if (!string.IsNullOrWhiteSpace(ruleJson))
        {
            return CategoryRuleDefinitionSerializer.Deserialize(ruleJson);
        }

        if (!string.IsNullOrWhiteSpace(matchText))
        {
            return CategoryRuleDefinitionFactory.LegacyContains(matchText);
        }

        throw new InvalidOperationException("Rule definition is required.");
    }

    private static string GetDisplayText(string? ruleJson, string? displayText, string? matchText)
    {
        if (!string.IsNullOrWhiteSpace(displayText))
        {
            return displayText;
        }

        return CategoryRuleDisplayFormatter.Format(GetDefinition(ruleJson, matchText));
    }

    private sealed record CompiledCategoryRule(Guid CategoryId, string MatchText, Func<Transaction, bool> Predicate);
}
