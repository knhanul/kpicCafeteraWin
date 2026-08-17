using KpicCafeteria.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KpicCafeteria.Infrastructure.Persistence.Configurations;

public sealed class PreservationRecordConfiguration : IEntityTypeConfiguration<PreservationRecord>
{
    public void Configure(EntityTypeBuilder<PreservationRecord> builder)
    {
        builder.ToTable("preservation_records");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.MealServiceId).HasColumnName("meal_service_id").IsRequired();
        builder.Property(x => x.CollectedAt).HasColumnName("collected_at");
        builder.Property(x => x.ManagerName).HasColumnName("manager_name").HasMaxLength(100);
        builder.Property(x => x.FreezerTemperature).HasColumnName("freezer_temperature").HasMaxLength(30);
        builder.Property(x => x.DisposalAt).HasColumnName("disposal_at");
        builder.Property(x => x.CollectorName).HasColumnName("collector_name").HasMaxLength(100);
        builder.Property(x => x.CollectionTime).HasColumnName("collection_time").HasMaxLength(20);
        builder.Property(x => x.Note).HasColumnName("note");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => x.MealServiceId).IsUnique();

        builder.HasOne(x => x.Service)
            .WithOne(x => x.Preservation)
            .HasForeignKey<PreservationRecord>(x => x.MealServiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
