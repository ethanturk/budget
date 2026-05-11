using BudgetApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetApp.Infrastructure.Persistence.Configurations;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).IsRequired();
        builder.Property(x => x.ProviderConnectionId).IsRequired();
        builder.Property(x => x.ProviderTransactionId).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Description).IsRequired();
        builder.Property(x => x.ImportedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasOne(x => x.Category)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.AccountId, x.Provider, x.ProviderConnectionId, x.ProviderTransactionId }).IsUnique();
        builder.HasIndex(x => new { x.AccountId, x.PostedAt });
    }
}
