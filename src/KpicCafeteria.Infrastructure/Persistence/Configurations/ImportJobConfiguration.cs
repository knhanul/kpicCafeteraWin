using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KpicCafeteria.Infrastructure.Persistence.Configurations;

public sealed class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> builder)
    {
        builder.ToTable("import_jobs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Token).HasColumnName("token").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Filename).HasColumnName("filename").HasMaxLength(255).IsRequired();
        builder.Property(x => x.StoragePath).HasColumnName("storage_path").HasMaxLength(600).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.Summary).HasColumnName("summary")
            .HasConversion<ValueConverters.JsonObjectConverter>();
        builder.Property(x => x.Errors).HasColumnName("errors")
            .HasConversion<ValueConverters.JsonListConverter>();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");

        builder.HasIndex(x => x.Token).IsUnique();
    }
}
