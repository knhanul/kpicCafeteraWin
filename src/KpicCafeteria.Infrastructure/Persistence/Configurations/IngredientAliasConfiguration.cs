using KpicCafeteria.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KpicCafeteria.Infrastructure.Persistence.Configurations;

public sealed class IngredientAliasConfiguration : IEntityTypeConfiguration<IngredientAlias>
{
    public void Configure(EntityTypeBuilder<IngredientAlias> builder)
    {
        builder.ToTable("ingredient_aliases");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Alias).HasColumnName("alias").HasMaxLength(200).IsRequired();
        builder.Property(x => x.IngredientId).HasColumnName("ingredient_id").IsRequired();
        builder.Property(x => x.Source).HasColumnName("source").HasMaxLength(80);

        builder.HasIndex(x => x.Alias).IsUnique();
        builder.HasIndex(x => x.IngredientId);

        builder.HasOne(x => x.Ingredient)
            .WithMany(x => x.Aliases)
            .HasForeignKey(x => x.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
