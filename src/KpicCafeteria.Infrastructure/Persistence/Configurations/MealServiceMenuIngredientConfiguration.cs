using KpicCafeteria.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KpicCafeteria.Infrastructure.Persistence.Configurations;

public sealed class MealServiceMenuIngredientConfiguration : IEntityTypeConfiguration<MealServiceMenuIngredient>
{
    public void Configure(EntityTypeBuilder<MealServiceMenuIngredient> builder)
    {
        builder.ToTable("meal_service_menu_ingredients");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.MealServiceMenuId).HasColumnName("meal_service_menu_id").IsRequired();
        builder.Property(x => x.IngredientId).HasColumnName("ingredient_id");
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(x => x.IngredientNameSnapshot).HasColumnName("ingredient_name_snapshot").HasMaxLength(200).IsRequired();
        builder.Property(x => x.QuantityTotal).HasColumnName("quantity_total");
        builder.Property(x => x.QuantityPer100).HasColumnName("quantity_per_100");
        builder.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(30);
        builder.Property(x => x.SourceNote).HasColumnName("source_note");
        builder.Property(x => x.SourceRow).HasColumnName("source_row");

        builder.HasIndex(x => x.MealServiceMenuId);
        builder.HasIndex(x => x.IngredientId);

        builder.HasOne(x => x.ServiceMenu)
            .WithMany(x => x.Ingredients)
            .HasForeignKey(x => x.MealServiceMenuId)
            .OnDelete(DeleteBehavior.Cascade);

        // 기준 재료 삭제 시 SET NULL — 스냅샷은 유지된다.
        builder.HasOne(x => x.Ingredient)
            .WithMany()
            .HasForeignKey(x => x.IngredientId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
