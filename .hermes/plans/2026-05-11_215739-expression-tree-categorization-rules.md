# Expression Tree Categorization Rules Implementation Plan

> **For Hermes:** Use subagent-driven-development skill to implement this plan task-by-task.

**Goal:** Replace the current single `MatchText` auto-categorization rules with richer, safe categorization rules backed by C# Expression Trees, so rules can match on payee, memo, description, amount, account, pending status, and date fields.

**Architecture:** Store rules as structured JSON definitions in Postgres, not arbitrary executable C# text. Convert those definitions into `Expression<Func<Transaction, bool>>` at apply time using `System.Linq.Expressions`, compile them for in-memory matching, and keep the rule shape explicit/safe. Preserve existing `MatchText` rules through migration/backward compatibility by translating them to an equivalent expression definition.

**Tech Stack:** ASP.NET Core / Blazor SSR, EF Core, PostgreSQL, `System.Linq.Expressions`, xUnit integration tests.

---

## Current Context

Relevant current files:

- `src/BudgetApp.Domain/Entities/CategoryRule.cs`
  - Current fields: `CategoryId`, `MatchText`, `IsActive`, timestamps.
- `src/BudgetApp.Infrastructure/Budgeting/AutoCategorizationService.cs`
  - `SaveRuleAsync(SaveCategoryRuleRequest)` stores simple text.
  - `ApplyRulesAsync()` loads active rules and checks `Description`, `Payee`, and `Memo` with case-insensitive `Contains`.
- `src/BudgetApp.Infrastructure/Persistence/Configurations/CategoryRuleConfiguration.cs`
  - Unique index on `{ CategoryId, MatchText }`.
- `src/BudgetApp.Web/Components/Pages/Budget.razor`
  - UI currently has one `Description contains` input.
- `src/BudgetApp.Web/Program.cs`
  - POST `/budget/category-rules` currently reads `categoryId` and `matchText`.
- Tests:
  - `tests/BudgetApp.IntegrationTests/Budgeting/AutoCategorizationServiceTests.cs`
  - `tests/BudgetApp.IntegrationTests/Budgeting/BudgetPageTests.cs`

Recent important context:

- Transactions now persist `Payee` and `Memo` from SimpleFIN.
- Search/categorization already considers `Description`, `Payee`, and `Memo`.
- The app uses local Postgres in Docker and EF migrations.

---

## Proposed Rule Model

### Do not execute arbitrary C#

Avoid accepting raw C# code from a textbox and compiling/running it. That creates security and operational risk even in a single-user self-hosted app.

Instead, use a constrained rule DSL that is stored as JSON and converted to Expression Trees internally.

### Initial supported rule definition

Add a structured rule definition that supports nested groups:

```csharp
public sealed record CategoryRuleDefinition(
    RuleNode Root);

public abstract record RuleNode;

public sealed record RuleGroupNode(
    RuleLogicalOperator Operator,
    IReadOnlyList<RuleNode> Children) : RuleNode;

public sealed record RuleConditionNode(
    RuleField Field,
    RuleComparison Comparison,
    string? Value) : RuleNode;

public enum RuleLogicalOperator
{
    And,
    Or
}

public enum RuleField
{
    Description,
    Payee,
    Memo,
    Amount,
    SpendingAmount,
    AccountName,
    PostedAt,
    TransactedAt,
    IsPending
}

public enum RuleComparison
{
    Contains,
    Equals,
    StartsWith,
    EndsWith,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    IsBlank,
    IsNotBlank,
    IsTrue,
    IsFalse
}
```

### Examples

Simple merchant rule:

```json
{
  "root": {
    "operator": "Or",
    "children": [
      { "field": "Payee", "comparison": "Contains", "value": "Kroger" },
      { "field": "Description", "comparison": "Contains", "value": "Kroger" }
    ]
  }
}
```

Interest charge rule:

```json
{
  "root": {
    "operator": "And",
    "children": [
      { "field": "Payee", "comparison": "Equals", "value": "Interest Charge" },
      { "field": "Amount", "comparison": "LessThan", "value": "0" }
    ]
  }
}
```

### Display name / summary

Store or compute a human-readable rule summary, for example:

- `Payee contains "Kroger" OR Description contains "Kroger"`
- `Payee equals "Interest Charge" AND Amount < 0`

This replaces `MatchText` as the main visible rule label.

---

## Schema Plan

Modify `CategoryRule`:

```csharp
public sealed class CategoryRule
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }

    // Keep temporarily for backward compatibility / migration.
    public string? MatchText { get; set; }

    public string RuleJson { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Category Category { get; set; } = null!;
}
```

Migration behavior:

1. Add nullable `RuleJson` and `DisplayText` columns.
2. Backfill existing rows from `MatchText` as:
   - `DisplayText`: `Description, Payee, or Memo contains "{MatchText}"`
   - `RuleJson`: an OR group over `Description`, `Payee`, and `Memo` `Contains`.
3. Make `RuleJson` and `DisplayText` required if straightforward in the migration.
4. Replace unique index `{ CategoryId, MatchText }` with `{ CategoryId, RuleJson }` or `{ CategoryId, DisplayText }`.
   - Prefer `{ CategoryId, RuleJson }` for behavior uniqueness.
   - Keep in mind JSON string ordering; deterministic serialization is required.
5. Consider dropping `MatchText` in a later cleanup migration after the feature is stable.

---

## Step-by-Step Plan

### Task 1: Add failing tests for expression-backed matching

**Objective:** Prove that rules can match on `Payee`, `Memo`, `Amount`, and combined AND/OR logic.

**Files:**

- Modify: `tests/BudgetApp.IntegrationTests/Budgeting/AutoCategorizationServiceTests.cs`

**Tests to add:**

1. `ApplyRulesAsync_AssignsRuleWhenExpressionMatchesPayeeAndAmount`
   - Seed transaction:
     - `Description = "INTEREST CHARGED ON PURCHASES"`
     - `Payee = "Interest Charge"`
     - `Amount = -701.91m`
   - Seed rule definition:
     - `Payee Equals "Interest Charge" AND Amount LessThan "0"`
   - Expected: transaction gets the selected category.

2. `ApplyRulesAsync_DoesNotAssignRuleWhenExpressionConditionFails`
   - Same payee but positive amount or different payee.
   - Expected: category remains null.

3. `ApplyRulesAsync_AssignsRuleWhenMemoContainsText`
   - Seed transaction with `Memo = "Invoice 1234"`.
   - Rule: `Memo Contains "invoice"`.
   - Expected: case-insensitive match.

**Run to verify RED:**

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/BudgetApp.IntegrationTests/BudgetApp.IntegrationTests.csproj --filter "FullyQualifiedName~AutoCategorizationServiceTests"
```

Expected: compile/test failures because rule definition types and API do not exist yet.

---

### Task 2: Add rule definition model and expression compiler

**Objective:** Introduce a small expression compiler that converts structured rule definitions into `Expression<Func<Transaction, bool>>`.

**Files:**

- Create: `src/BudgetApp.Infrastructure/Budgeting/Rules/CategoryRuleDefinition.cs`
- Create: `src/BudgetApp.Infrastructure/Budgeting/Rules/CategoryRuleExpressionCompiler.cs`
- Create: `tests/BudgetApp.UnitTests/CategoryRuleExpressionCompilerTests.cs` or use integration tests if unit project does not reference needed domain types.

**Implementation outline:**

```csharp
public sealed class CategoryRuleExpressionCompiler
{
    public Expression<Func<Transaction, bool>> BuildExpression(CategoryRuleDefinition definition)
    {
        var transaction = Expression.Parameter(typeof(Transaction), "transaction");
        var body = BuildNode(definition.Root, transaction);
        return Expression.Lambda<Func<Transaction, bool>>(body, transaction);
    }

    public Func<Transaction, bool> Compile(CategoryRuleDefinition definition) =>
        BuildExpression(definition).Compile();
}
```

Rules:

- String comparisons are case-insensitive.
- Null strings are treated as non-matches except `IsBlank`.
- `SpendingAmount` means `-Amount`, useful for user-facing spending thresholds.
- Decimal comparisons parse values with `CultureInfo.InvariantCulture`.
- Date comparisons can be deferred unless needed immediately; define enums now only if tests cover them.

**Run targeted tests:**

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test --filter "FullyQualifiedName~CategoryRuleExpressionCompilerTests|FullyQualifiedName~AutoCategorizationServiceTests"
```

Expected after implementation: new compiler tests pass, service tests may still fail until service is wired.

---

### Task 3: Persist rule JSON and display text

**Objective:** Extend `CategoryRule` and EF mapping to store expression rules.

**Files:**

- Modify: `src/BudgetApp.Domain/Entities/CategoryRule.cs`
- Modify: `src/BudgetApp.Infrastructure/Persistence/Configurations/CategoryRuleConfiguration.cs`
- Generate: `src/BudgetApp.Infrastructure/Persistence/Migrations/*_AddCategoryRuleExpressions.cs`
- Modify snapshot: `src/BudgetApp.Infrastructure/Persistence/Migrations/BudgetAppDbContextModelSnapshot.cs` generated by EF.

**Migration command:**

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet ef migrations add AddCategoryRuleExpressions \
  --project src/BudgetApp.Infrastructure/BudgetApp.Infrastructure.csproj \
  --startup-project src/BudgetApp.Web/BudgetApp.Web.csproj \
  --output-dir Persistence/Migrations
```

**Backfill requirement:**

Edit the generated migration to backfill existing rows. Use deterministic JSON. Existing `MatchText = "KROGER"` should become an OR group equivalent to:

- `Description contains KROGER`
- `Payee contains KROGER`
- `Memo contains KROGER`

**Verification:**

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/BudgetApp.IntegrationTests/BudgetApp.IntegrationTests.csproj --filter "FullyQualifiedName~AutoCategorizationServiceTests"
```

---

### Task 4: Replace service save/apply logic with expression rules

**Objective:** Make `AutoCategorizationService` save and apply expression-backed rules while retaining compatibility for existing callers.

**Files:**

- Modify: `src/BudgetApp.Infrastructure/Budgeting/AutoCategorizationService.cs`
- Modify/add: tests in `tests/BudgetApp.IntegrationTests/Budgeting/AutoCategorizationServiceTests.cs`

**API proposal:**

```csharp
public sealed record SaveCategoryRuleRequest(
    Guid CategoryId,
    CategoryRuleDefinition Definition);
```

Temporary compatibility option:

```csharp
public static SaveCategoryRuleRequest FromLegacyMatchText(Guid categoryId, string matchText)
```

or add a separate service method:

```csharp
public Task<SaveCategoryRuleResult> SaveLegacyContainsRuleAsync(...)
```

**Apply behavior:**

- Load active non-archived category rules ordered by `CreatedAt`.
- Deserialize `RuleJson` to `CategoryRuleDefinition`.
- Compile each rule once per apply call.
- Load uncategorized, non-pending transactions with needed navigation fields if account fields are supported.
- For each transaction, use first matching rule.
- Save category and update timestamp.

**Important:** If supporting `AccountName`, include the account relation or project enough account data before matching.

**Run targeted tests:**

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/BudgetApp.IntegrationTests/BudgetApp.IntegrationTests.csproj --filter "FullyQualifiedName~AutoCategorizationServiceTests"
```

---

### Task 5: Update rule list model and UI display

**Objective:** Show expression rule summaries in the budget UI instead of only raw match text.

**Files:**

- Modify: `src/BudgetApp.Infrastructure/Budgeting/AutoCategorizationService.cs`
  - `CategoryRuleListItem` should expose `DisplayText` and perhaps `RuleJson` if needed.
- Modify: `src/BudgetApp.Web/Components/Pages/Budget.razor`
  - Display rule summary: `DisplayText → Group — Category`.

**Tests:**

- Update `GetRulesAsync_ReturnsRulesWithCategoryAndGroupNames` to assert display text.
- If existing page tests check labels, update them.

**Run:**

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/BudgetApp.IntegrationTests/BudgetApp.IntegrationTests.csproj --filter "FullyQualifiedName~AutoCategorizationServiceTests|FullyQualifiedName~BudgetPageTests"
```

---

### Task 6: Replace simple match-text form with an expression rule builder form

**Objective:** Let the user create useful expression rules from the UI without typing raw JSON.

**Initial UI scope:** One rule can have up to three conditions joined by `AND` or `OR`.

Fields per condition:

- Field select:
  - `Payee`
  - `Description`
  - `Memo`
  - `Amount`
  - `Spending amount`
  - optionally `Account name`
- Comparison select filtered by field type:
  - String: `contains`, `equals`, `starts with`, `ends with`, `is blank`, `is not blank`
  - Decimal: `<`, `<=`, `=`, `>=`, `>`
- Value input where needed.

**Files:**

- Modify: `src/BudgetApp.Web/Components/Pages/Budget.razor`
- Modify: `src/BudgetApp.Web/Program.cs`
- Possibly create helper: `src/BudgetApp.Web/Budgeting/CategoryRuleFormParser.cs` if parsing is too large for `Program.cs`.

**Endpoint form fields proposal:**

- `categoryId`
- `logicalOperator`
- `conditions[0].field`
- `conditions[0].comparison`
- `conditions[0].value`
- `conditions[1].field`
- ...

Since minimal APIs with raw forms do not bind arrays automatically, parse explicit field names manually or use simple names:

- `field1`, `comparison1`, `value1`
- `field2`, `comparison2`, `value2`
- `field3`, `comparison3`, `value3`
- `logicalOperator`

**Validation:**

- Category is required.
- At least one complete condition is required.
- String comparisons requiring a value must have a nonblank value.
- Decimal comparisons must parse a decimal.
- `IsBlank`, `IsNotBlank`, `IsTrue`, `IsFalse` ignore value.

**Tests:**

- Add POST test for valid expression form creates rule.
- Add POST test for missing/invalid condition returns `400`.

Run:

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/BudgetApp.IntegrationTests/BudgetApp.IntegrationTests.csproj --filter "FullyQualifiedName~BudgetPageTests"
```

---

### Task 7: Add quick-create shortcuts from transactions

**Objective:** Make categorization workflow practical by adding shortcuts to create a rule from a transaction’s payee/description/memo.

**Files:**

- Modify: `src/BudgetApp.Web/Components/Pages/Budget.razor`
- Modify/add endpoint in `src/BudgetApp.Web/Program.cs` if needed.

**Possible UI behavior:**

In each uncategorized transaction card, add buttons/dropdowns:

- `Create payee rule` using `Payee Equals <payee>`
- `Create description rule` using `Description Contains <description>`
- `Create memo rule` if memo exists

This can be a second phase if the generic rule builder is enough.

**Tests:**

- Integration test that posts a transaction-derived payee rule and confirms it appears in rule list.

---

### Task 8: Apply migration to local Postgres and verify with real data

**Objective:** Validate the migration and rule matching against the local Docker Postgres database.

**Commands:**

```bash
export PATH="$HOME/.dotnet:$PATH"
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update \
  --project src/BudgetApp.Infrastructure/BudgetApp.Infrastructure.csproj \
  --startup-project src/BudgetApp.Web/BudgetApp.Web.csproj
```

Inspect rules:

```bash
docker exec budget-app-postgres psql -U postgres -d budget_app_dev \
  -c "select \"MatchText\", \"DisplayText\", left(\"RuleJson\", 120) from category_rules order by \"CreatedAt\";"
```

Then run full tests:

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test
```

Expected:

- Existing rules are preserved/backfilled.
- Unit tests pass.
- Integration tests pass.

---

### Task 9: Restart LAN app and smoke test

**Objective:** Run the app with the updated code on the LAN and verify the page loads.

**Commands:**

```bash
ss -ltnp | grep ':5288' || true
# kill existing BudgetApp.Web pid if needed

export PATH="$HOME/.dotnet:$PATH"
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://0.0.0.0:5288 \
  dotnet run --project src/BudgetApp.Web/BudgetApp.Web.csproj --no-launch-profile
```

Smoke checks:

```bash
curl -I --max-time 5 http://127.0.0.1:5288/
ss -ltnp | grep ':5288'
```

Expected:

- App redirects unauthenticated `/` to login.
- Kestrel listens on `0.0.0.0:5288`.
- LAN URL remains `http://192.168.5.203:5288`.

---

### Task 10: Commit and push

**Objective:** Save the feature after tests/migration/app smoke checks pass.

**Commands:**

```bash
git status --short
git add src tests
git commit -m "Add expression tree category rules"
git push
```

---

## Acceptance Criteria

- A category rule can be created from structured fields rather than only a single match text.
- Rule matching is powered by `Expression<Func<Transaction, bool>>` built from a safe rule definition.
- Rules can match at least:
  - `Payee contains/equals`
  - `Description contains/equals`
  - `Memo contains/equals`
  - `Amount` decimal comparisons
- Rules can combine conditions with `AND` and `OR`.
- Existing `MatchText` rules still work after migration.
- Budget UI displays readable rule summaries.
- Invalid rule definitions are rejected with a clear error.
- Full `dotnet test` passes.
- EF migration applies to local Postgres.
- App restarts successfully on `http://192.168.5.203:5288`.

---

## Risks and Tradeoffs

- **Raw C# expressions:** Powerful but unsafe and harder to validate. Avoid for now.
- **JSON uniqueness:** Equivalent JSON with different property ordering could bypass uniqueness. Use deterministic serialization before saving.
- **EF translation vs compiled expressions:** Expression trees could theoretically be translated to SQL, but current apply flow loads candidate uncategorized transactions and matches in memory. That is simpler and fine for personal-budget scale. Revisit if transaction count grows large.
- **UI complexity:** A full nested expression builder is overkill. Start with one group of 1–3 conditions and a single `AND`/`OR` operator.
- **Backwards compatibility:** Keep `MatchText` during the first rollout to reduce migration risk; remove later if desired.

---

## Decisions

1. Use a simple UI builder only; do not add an advanced JSON editor in this slice.
2. Keep rule ordering as `CreatedAt` order for now.
3. Do not include account/institution matching in the initial expression-rule scope.
4. Defer rule preview, but track it as the next backlog item after expression rules.

## Follow-up Backlog Item

Add a rule-preview feature after expression rules are implemented:

- On the rule builder, show how many current uncategorized posted transactions would match before saving.
- Ideally show a small sample of matching transactions with payee, description, memo, posted date, and amount.
- Reuse the same expression compiler used by rule application so preview and apply behavior stay identical.
