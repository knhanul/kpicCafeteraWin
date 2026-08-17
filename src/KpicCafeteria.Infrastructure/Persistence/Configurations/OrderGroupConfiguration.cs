using KpicCafeteria.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KpicCafeteria.Infrastructure.Persistence.Configurations;

public sealed class OrderGroupConfiguration : IEntityTypeConfiguration<OrderGroup>
{
    public void Configure(EntityTypeBuilder<OrderGroup> builder)
    {
        builder.ToTable("order_groups");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.IngredientId).HasColumnName("ingredient_id");
        builder.Property(x => x.IngredientNameSnapshot).HasColumnName("ingredient_name_snapshot").HasMaxLength(200).IsRequired();
        builder.Property(x => x.OrderQuantity).HasColumnName("order_quantity");
        builder.Property(x => x.OrderUnit).HasColumnName("order_unit").HasMaxLength(30);
        builder.Property(x => x.OrderDate).HasColumnName("order_date");
        builder.Property(x => x.DeliveryDate).HasColumnName("delivery_date");
        builder.Property(x => x.TotalRequiredQuantity).HasColumnName("total_required_quantity");
        builder.Property(x => x.RequiredUnit).HasColumnName("required_unit").HasMaxLength(30);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(80);

        builder.HasIndex(x => x.IngredientId);

        builder.HasOne<Ingredient>()
            .WithMany()
            .HasForeignKey(x => x.IngredientId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
