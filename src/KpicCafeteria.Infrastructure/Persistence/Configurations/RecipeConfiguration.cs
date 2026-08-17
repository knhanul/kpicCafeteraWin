using KpicCafeteria.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KpicCafeteria.Infrastructure.Persistence.Configurations;

public sealed class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.ToTable("recipes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.MenuId).HasColumnName("menu_id").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").IsRequired();
        builder.Property(x => x.CompositionKey).HasColumnName("composition_key").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Note).HasColumnName("note");
        builder.Property(x => x.IsDefault).HasColumnName("is_default").IsRequired();
        builder.Property(x => x.Active).HasColumnName("active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => x.MenuId);
        builder.HasIndex(x => x.CompositionKey);
        builder.HasIndex(x => x.IsDefault);
        builder.HasIndex(x => x.Active);

        builder.HasIndex(x => new { x.MenuId, x.Version }).IsUnique().HasDatabaseName("uq_recipe_menu_version");
        builder.HasIndex(x => new { x.MenuId, x.CompositionKey }).IsUnique().HasDatabaseName("uq_recipe_menu_composition");

        builder.HasOne(x => x.Menu)
            .WithMany(x => x.Recipes)
            .HasForeignKey(x => x.MenuId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Ingredients)
            .WithOne(x => x.Recipe)
            .HasForeignKey(x => x.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
