using BudgetApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetApp.Infrastructure.Persistence.Configurations;

internal sealed class BudgetMonthConfiguration : IEntityTypeConfiguration<BudgetMonth>
{
    public void Configure(EntityTypeBuilder<BudgetMonth> builder)
    {
        builder.ToTable("budget_months");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Month).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasMany(x => x.BudgetAllocations)
            .WithOne(x => x.BudgetMonth)
            .HasForeignKey(x => x.BudgetMonthId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Month).IsUnique();
    }
}
