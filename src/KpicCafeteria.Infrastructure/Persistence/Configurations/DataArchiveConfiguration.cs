using KpicCafeteria.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KpicCafeteria.Infrastructure.Persistence.Configurations;

public sealed class DataArchiveConfiguration : IEntityTypeConfiguration<DataArchive>
{
    public void Configure(EntityTypeBuilder<DataArchive> builder)
    {
        builder.ToTable("data_archives");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Filename).HasColumnName("filename").HasMaxLength(255).IsRequired();
        builder.Property(x => x.StoredFilename).HasColumnName("stored_filename").HasMaxLength(255).IsRequired();
        builder.Property(x => x.FileSize).HasColumnName("file_size");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.DateFrom).HasColumnName("date_from");
        builder.Property(x => x.DateTo).HasColumnName("date_to");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
    }
}
