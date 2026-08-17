using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KpicCafeteria.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(x => x.Id);

        // Windows 버전은 users 테이블이 없으므로 FK를 만들지 않는다.
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(80).IsRequired();
        builder.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(80);
        builder.Property(x => x.EntityId).HasColumnName("entity_id").HasMaxLength(80);
        builder.Property(x => x.Detail).HasColumnName("detail")
            .HasConversion<ValueConverters.JsonObjectConverter>();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.Action);
    }
}
