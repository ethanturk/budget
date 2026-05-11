using BudgetApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetApp.Infrastructure.Persistence.Configurations;

internal sealed class CategoryGroupConfiguration : IEntityTypeConfiguration<CategoryGroup>
{
    public void Configure(EntityTypeBuilder<CategoryGroup> builder)
    {
        builder.ToTable("category_groups");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasMany(x => x.Categories)
            .WithOne(x => x.CategoryGroup)
            .HasForeignKey(x => x.CategoryGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Name).IsUnique();
    }
}
