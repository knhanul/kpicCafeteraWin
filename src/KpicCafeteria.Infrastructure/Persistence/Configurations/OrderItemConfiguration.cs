using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KpicCafeteria.Infrastructure.Persistence.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ServiceDate).HasColumnName("service_date").IsRequired();
        builder.Property(x => x.IngredientId).HasColumnName("ingredient_id");
        builder.Property(x => x.IngredientNameSnapshot).HasColumnName("ingredient_name_snapshot").HasMaxLength(200).IsRequired();
        builder.Property(x => x.RequiredQuantity).HasColumnName("required_quantity");
        builder.Property(x => x.RequiredUnit).HasColumnName("required_unit").HasMaxLength(30);
        builder.Property(x => x.OrderQuantity).HasColumnName("order_quantity");
        builder.Property(x => x.OrderUnit).HasColumnName("order_unit").HasMaxLength(30);
        builder.Property(x => x.OrderDate).HasColumnName("order_date");
        builder.Property(x => x.DeliveryDate).HasColumnName("delivery_date");
        builder.Property(x => x.OrderNote).HasColumnName("order_note").HasMaxLength(500);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired()
            .HasConversion(ValueConverters.OrderStatusConverter);
        builder.Property(x => x.OrderGroupId).HasColumnName("order_group_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => x.ServiceDate);
        builder.HasIndex(x => x.IngredientId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.OrderGroupId);

        // 기존 models.py: (service_date, ingredient_id) UNIQUE.
        // SQLite는 NULL을 서로 다른 값으로 취급하므로 ingredient_id가 NULL인 행은
        // 같은 service_date에 여러 개 존재할 수 있다. (업무 규칙: (service_date, 재료명 스냅샷)으로 구분)
        builder.HasIndex(x => new { x.ServiceDate, x.IngredientId }).IsUnique().HasDatabaseName("uq_order_item_date_ingredient");

        builder.HasOne(x => x.Ingredient)
            .WithMany()
            .HasForeignKey(x => x.IngredientId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.OrderGroup)
            .WithMany()
            .HasForeignKey(x => x.OrderGroupId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
