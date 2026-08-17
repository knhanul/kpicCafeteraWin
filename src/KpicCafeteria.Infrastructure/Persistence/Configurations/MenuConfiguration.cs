using KpicCafeteria.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KpicCafeteria.Infrastructure.Persistence.Configurations;

public sealed class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
    public void Configure(EntityTypeBuilder<Menu> builder)
    {
        builder.ToTable("menus");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceCode).HasColumnName("source_code").HasMaxLength(30);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.CanonicalName).HasColumnName("canonical_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Role).HasColumnName("role").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Active).HasColumnName("active").IsRequired();
        builder.Property(x => x.ReviewStatus).HasColumnName("review_status").HasMaxLength(40).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => x.SourceCode).IsUnique();
        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasIndex(x => x.CanonicalName);
        builder.HasIndex(x => x.Role);

        builder.HasMany(x => x.Recipes)
            .WithOne(x => x.Menu)
            .HasForeignKey(x => x.MenuId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
