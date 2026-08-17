using KpicCafeteria.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KpicCafeteria.Infrastructure.Persistence.Configurations;

public sealed class BackupRecordConfiguration : IEntityTypeConfiguration<BackupRecord>
{
    public void Configure(EntityTypeBuilder<BackupRecord> builder)
    {
        builder.ToTable("backup_records");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Filename).HasColumnName("filename").HasMaxLength(255).IsRequired();
        builder.Property(x => x.StoredFilename).HasColumnName("stored_filename").HasMaxLength(255).IsRequired();
        builder.Property(x => x.FileSize).HasColumnName("file_size");
        builder.Property(x => x.BackupType).HasColumnName("backup_type").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.ChecksumSha256).HasColumnName("checksum_sha256").HasMaxLength(64);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(80);
    }
}
