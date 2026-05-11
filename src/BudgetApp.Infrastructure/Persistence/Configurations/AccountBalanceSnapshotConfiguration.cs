using BudgetApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetApp.Infrastructure.Persistence.Configurations;

internal sealed class AccountBalanceSnapshotConfiguration : IEntityTypeConfiguration<AccountBalanceSnapshot>
{
    public void Configure(EntityTypeBuilder<AccountBalanceSnapshot> builder)
    {
        builder.ToTable("account_balance_snapshots");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.AsOfAt).IsRequired();
        builder.Property(x => x.CurrentAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.AvailableAmount).HasPrecision(18, 2);
        builder.Property(x => x.LimitAmount).HasPrecision(18, 2);
        builder.Property(x => x.CurrencyCode).IsRequired();

        builder.HasOne(x => x.Account)
            .WithMany(x => x.BalanceSnapshots)
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SyncRun)
            .WithMany()
            .HasForeignKey(x => x.SyncRunId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.AccountId, x.AsOfAt }).IsUnique();
    }
}
