using KpicCafeteria.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KpicCafeteria.Infrastructure.Persistence.Configurations;

public sealed class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> builder)
    {
        builder.ToTable("recipe_ingredients");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RecipeId).HasColumnName("recipe_id").IsRequired();
        builder.Property(x => x.IngredientId).HasColumnName("ingredient_id").IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(x => x.QuantityPer100).HasColumnName("quantity_per_100");
        builder.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(30);
        builder.Property(x => x.IsPrimary).HasColumnName("is_primary").IsRequired();
        builder.Property(x => x.ReviewStatus).HasColumnName("review_status").HasMaxLength(60).IsRequired();

        builder.HasIndex(x => x.RecipeId);
        builder.HasIndex(x => x.IngredientId);

        builder.HasOne(x => x.Recipe)
            .WithMany(x => x.Ingredients)
            .HasForeignKey(x => x.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        // 기존 models.py: ingredient_id FK는 ondelete 미지정 (NO ACTION).
        // 재료는 미사용 처리이므로 물리 삭제 시 제약 위반이 발생한다.
        builder.HasOne(x => x.Ingredient)
            .WithMany()
            .HasForeignKey(x => x.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
