using BudgetApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetApp.Infrastructure.Persistence.Configurations;

internal sealed class SimpleFinConnectionConfiguration : IEntityTypeConfiguration<SimpleFinConnection>
{
    public void Configure(EntityTypeBuilder<SimpleFinConnection> builder)
    {
        builder.ToTable("simplefin_connections");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).IsRequired();
        builder.Property(x => x.AccessUrlCiphertext).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
    }
}
