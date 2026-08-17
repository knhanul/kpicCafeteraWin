using KpicCafeteria.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KpicCafeteria.Infrastructure.Persistence.Configurations;

public sealed class MealServiceMenuConfiguration : IEntityTypeConfiguration<MealServiceMenu>
{
    public void Configure(EntityTypeBuilder<MealServiceMenu> builder)
    {
        builder.ToTable("meal_service_menus");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.MealServiceId).HasColumnName("meal_service_id").IsRequired();
        builder.Property(x => x.MenuId).HasColumnName("menu_id");
        builder.Property(x => x.RecipeId).HasColumnName("recipe_id");
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(x => x.MenuNameSnapshot).HasColumnName("menu_name_snapshot").HasMaxLength(200).IsRequired();
        builder.Property(x => x.RecipeNameSnapshot).HasColumnName("recipe_name_snapshot").HasMaxLength(120);
        builder.Property(x => x.RecipeVersionSnapshot).HasColumnName("recipe_version_snapshot");
        builder.Property(x => x.Note).HasColumnName("note");
        builder.Property(x => x.IsRepresentative).HasColumnName("is_representative").IsRequired();
        builder.Property(x => x.CookingInstruction).HasColumnName("cooking_instruction");
        builder.Property(x => x.CookingNote).HasColumnName("cooking_note");

        builder.HasIndex(x => x.MealServiceId);
        builder.HasIndex(x => x.MenuId);
        builder.HasIndex(x => x.RecipeId);

        builder.HasOne(x => x.Service)
            .WithMany(x => x.Menus)
            .HasForeignKey(x => x.MealServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // 기준 메뉴/레시피 삭제 시 SET NULL — 스냅샷은 유지된다.
        builder.HasOne(x => x.Menu)
            .WithMany()
            .HasForeignKey(x => x.MenuId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.SourceRecipe)
            .WithMany()
            .HasForeignKey(x => x.RecipeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Ingredients)
            .WithOne(x => x.ServiceMenu)
            .HasForeignKey(x => x.MealServiceMenuId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
