using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KpicCafeteria.Infrastructure.Persistence.Configurations;

public sealed class MealServiceConfiguration : IEntityTypeConfiguration<MealService>
{
    public void Configure(EntityTypeBuilder<MealService> builder)
    {
        builder.ToTable("meal_services");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ServiceDate).HasColumnName("service_date").IsRequired();
        builder.Property(x => x.MealType).HasColumnName("meal_type").HasMaxLength(30).IsRequired()
            .HasConversion(ValueConverters.MealType);
        builder.Property(x => x.PlannedCount).HasColumnName("planned_count").IsRequired();
        builder.Property(x => x.ServiceTime).HasColumnName("service_time");
        builder.Property(x => x.ConceptTitle).HasColumnName("concept_title").HasMaxLength(80);
        builder.Property(x => x.Note).HasColumnName("note");
        builder.Property(x => x.MealPlanOutputAt).HasColumnName("meal_plan_output_at");
        builder.Property(x => x.CookingOutputAt).HasColumnName("cooking_output_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => x.ServiceDate);
        builder.HasIndex(x => x.MealType);

        builder.HasIndex(x => new { x.ServiceDate, x.MealType }).IsUnique().HasDatabaseName("uq_meal_service_date_type");

        builder.HasMany(x => x.Menus)
            .WithOne(x => x.Service)
            .HasForeignKey(x => x.MealServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Preservation)
            .WithOne(x => x.Service)
            .HasForeignKey<PreservationRecord>(x => x.MealServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Actual)
            .WithOne(x => x.Service)
            .HasForeignKey<MealActual>(x => x.MealServiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
