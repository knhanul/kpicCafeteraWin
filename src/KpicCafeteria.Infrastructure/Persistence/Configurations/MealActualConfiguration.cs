using KpicCafeteria.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KpicCafeteria.Infrastructure.Persistence.Configurations;

public sealed class MealActualConfiguration : IEntityTypeConfiguration<MealActual>
{
    public void Configure(EntityTypeBuilder<MealActual> builder)
    {
        builder.ToTable("meal_actuals");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.MealServiceId).HasColumnName("meal_service_id").IsRequired();
        builder.Property(x => x.ActualCount).HasColumnName("actual_count");
        builder.Property(x => x.Note).HasColumnName("note");
        builder.Property(x => x.RecordedAt).HasColumnName("recorded_at");

        builder.HasIndex(x => x.MealServiceId).IsUnique();

        builder.HasOne(x => x.Service)
            .WithOne(x => x.Actual)
            .HasForeignKey<MealActual>(x => x.MealServiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
