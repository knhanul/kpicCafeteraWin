using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KpicCafeteria.Infrastructure.Persistence.Configurations;

public sealed class DocumentTemplateConfiguration : IEntityTypeConfiguration<DocumentTemplate>
{
    public void Configure(EntityTypeBuilder<DocumentTemplate> builder)
    {
        builder.ToTable("document_templates");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DocumentType).HasColumnName("document_type").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.OriginalFilename).HasColumnName("original_filename").HasMaxLength(255).IsRequired();
        builder.Property(x => x.StoredFilename).HasColumnName("stored_filename").HasMaxLength(255);
        builder.Property(x => x.StoragePath).HasColumnName("storage_path").HasMaxLength(600).IsRequired();
        builder.Property(x => x.FileSize).HasColumnName("file_size");
        builder.Property(x => x.ChecksumSha256).HasColumnName("checksum_sha256").HasMaxLength(64);
        builder.Property(x => x.Active).HasColumnName("active").IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").IsRequired();
        builder.Property(x => x.IsValid).HasColumnName("is_valid").IsRequired();
        builder.Property(x => x.ValidationMessage).HasColumnName("validation_message");
        builder.Property(x => x.PlaceholderSummary).HasColumnName("placeholder_summary")
            .HasConversion<ValueConverters.JsonObjectConverter>();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(80);

        builder.HasIndex(x => x.DocumentType);
        builder.HasIndex(x => x.Active);
    }
}
