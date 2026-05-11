using BudgetApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetApp.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).IsRequired();
        builder.Property(x => x.ProviderConnectionId).IsRequired();
        builder.Property(x => x.ProviderAccountId).IsRequired();
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.CurrencyCode).IsRequired();
        builder.Property(x => x.AccountType).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasOne(x => x.Connection)
            .WithMany(x => x.Accounts)
            .HasForeignKey(x => x.ConnectionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Institution)
            .WithMany(x => x.Accounts)
            .HasForeignKey(x => x.InstitutionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Transactions)
            .WithOne(x => x.Account)
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ConnectionId);
        builder.HasIndex(x => new { x.Provider, x.ProviderConnectionId, x.ProviderAccountId }).IsUnique();
    }
}
