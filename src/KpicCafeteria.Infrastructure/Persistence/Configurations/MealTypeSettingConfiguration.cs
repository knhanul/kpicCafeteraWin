using KpicCafeteria.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KpicCafeteria.Infrastructure.Persistence.Configurations;

public sealed class MealTypeSettingConfiguration : IEntityTypeConfiguration<MealTypeSetting>
{
    public void Configure(EntityTypeBuilder<MealTypeSetting> builder)
    {
        builder.ToTable("meal_type_settings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(30).IsRequired();
        builder.Property(x => x.DefaultPlannedCount).HasColumnName("default_planned_count").IsRequired();
        builder.Property(x => x.DefaultServiceTime).HasColumnName("default_service_time");
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(x => x.Active).HasColumnName("active").IsRequired();
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.Name).IsUnique();
    }
}
