using KpicCafeteria.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KpicCafeteria.Infrastructure.Persistence.Configurations;

public sealed class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> builder)
    {
        builder.ToTable("ingredients");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceCode).HasColumnName("source_code").HasMaxLength(30);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.StatGroup).HasColumnName("stat_group").HasMaxLength(60).IsRequired();
        builder.Property(x => x.DefaultUnit).HasColumnName("default_unit").HasMaxLength(30);
        builder.Property(x => x.PurchasePackageQuantity).HasColumnName("purchase_package_quantity");
        builder.Property(x => x.PurchasePackageUnit).HasColumnName("purchase_package_unit").HasMaxLength(30);
        builder.Property(x => x.KgFactor).HasColumnName("kg_factor");
        builder.Property(x => x.AnalysisExcluded).HasColumnName("analysis_excluded").IsRequired();
        builder.Property(x => x.Active).HasColumnName("active").IsRequired();
        builder.Property(x => x.ReviewStatus).HasColumnName("review_status").HasMaxLength(40).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => x.SourceCode).IsUnique();
        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasIndex(x => x.StatGroup);

        builder.HasMany(x => x.Aliases)
            .WithOne(x => x.Ingredient)
            .HasForeignKey(x => x.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
