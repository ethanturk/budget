using BudgetApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetApp.Infrastructure.Persistence.Configurations;

internal sealed class SyncRunConfiguration : IEntityTypeConfiguration<SyncRun>
{
    public void Configure(EntityTypeBuilder<SyncRun> builder)
    {
        builder.ToTable("sync_runs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.TriggerSource).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.StartedAt).IsRequired();

        builder.HasOne(x => x.Connection)
            .WithMany(x => x.SyncRuns)
            .HasForeignKey(x => x.ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ConnectionId, x.StartedAt });
    }
}
