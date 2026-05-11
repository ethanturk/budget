using BudgetApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.Infrastructure.Persistence;

public sealed class BudgetAppDbContext : DbContext
{
    public BudgetAppDbContext(DbContextOptions<BudgetAppDbContext> options)
        : base(options)
    {
    }

    public DbSet<SimpleFinConnection> SimpleFinConnections => Set<SimpleFinConnection>();
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountBalanceSnapshot> AccountBalanceSnapshots => Set<AccountBalanceSnapshot>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<CategoryGroup> CategoryGroups => Set<CategoryGroup>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<BudgetMonth> BudgetMonths => Set<BudgetMonth>();
    public DbSet<BudgetAllocation> BudgetAllocations => Set<BudgetAllocation>();
    public DbSet<CategoryRule> CategoryRules => Set<CategoryRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BudgetAppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
