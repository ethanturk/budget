using BudgetApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetApp.Infrastructure.Persistence.Configurations;

internal sealed class InstitutionConfiguration : IEntityTypeConfiguration<Institution>
{
    public void Configure(EntityTypeBuilder<Institution> builder)
    {
        builder.ToTable("institutions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).IsRequired();
        builder.Property(x => x.ProviderInstitutionId).IsRequired();
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => new { x.Provider, x.ProviderInstitutionId }).IsUnique();
    }
}
