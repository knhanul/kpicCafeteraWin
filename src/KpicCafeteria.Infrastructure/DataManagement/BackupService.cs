using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using KpicCafeteria.Application.Abstractions;
using KpicCafeteria.Application.DataManagement;
using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace KpicCafeteria.Infrastructure.DataManagement;

/// <summary>시스템 백업 서비스 구현.</summary>
public sealed class BackupService : IBackupService
{
    private const int AutoRetentionCount = 30;
    private const string ManifestName = "manifest.json";

    private readonly IAppDataPathProvider _paths;
    private readonly IDbContextFactory<CafeteriaDbContext> _factory;

    public BackupService(IAppDataPathProvider paths, IDbContextFactory<CafeteriaDbContext> factory)
    {
        _paths = paths;
        _factory = factory;
    }

    public async Task<BackupInfo> CreateManualBackupAsync(CancellationToken cancellationToken = default)
        => await CreateBackupAsync(BackupType.Manual, cancellationToken);

    public async Task<BackupInfo?> EnsureAutoBackupAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var lastAuto = await db.BackupRecords
            .Where(b => b.BackupType == BackupType.Auto.ToString())
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastAuto is not null && (DateTime.UtcNow - lastAuto.CreatedAt).TotalHours < 24)
            return null;

        return await CreateBackupAsync(BackupType.Auto, cancellationToken);
    }

    public async Task<BackupInfo> CreatePreRestoreBackupAsync(CancellationToken cancellationToken = default)
        => await CreateBackupAsync(BackupType.PreRestore, cancellationToken);

    public async Task<IReadOnlyList<BackupInfo>> ListBackupsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = await db.BackupRecords
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<int> CleanupAutoBackupsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var autos = await db.BackupRecords
            .Where(b => b.BackupType == BackupType.Auto.ToString())
            .OrderByDescending(b => b.CreatedAt)
            .Skip(AutoRetentionCount)
            .ToListAsync(cancellationToken);

        foreach (var record in autos)
        {
            if (File.Exists(record.StoredFilename))
            {
                try
                {
                    File.Delete(record.StoredFilename);
                }
                catch
                {
                    // ignore
                }
            }
            db.BackupRecords.Remove(record);
        }

        await db.SaveChangesAsync(cancellationToken);
        return autos.Count;
    }

    private async Task<BackupInfo> CreateBackupAsync(BackupType type, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_paths.BackupDirectory);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var typeName = type switch
        {
            BackupType.Auto => "auto",
            BackupType.PreRestore => "pre_restore",
            _ => "manual",
        };
        var filename = $"KpicCafeteria_Backup_{typeName}_{timestamp}.kpicbackup";
        var packagePath = Path.Combine(_paths.BackupDirectory, filename);
        var tempDir = Path.Combine(_paths.TempDirectory, $"backup-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDir);
            var dbCopyPath = Path.Combine(tempDir, "cafeteria.db");
            await CreateDatabaseCopyAsync(_paths.DatabasePath, dbCopyPath, cancellationToken);

            // 개별 파일 체크섬은 백업 패키지의 zip 엔트리 무결성으로 대체되며,
            // 복구 시 PRAGMA integrity_check로 DB 무결성을 검증한다.
            var checksums = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var templateFiles = new List<string>();
            if (Directory.Exists(_paths.TemplateDirectory))
            {
                templateFiles.AddRange(Directory.EnumerateFiles(_paths.TemplateDirectory, "*", SearchOption.AllDirectories));
            }

            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var schemaVersion = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).LastOrDefault() ?? "0";

            var manifest = new BackupManifest
            {
                BackupVersion = 1,
                CreatedAt = DateTime.UtcNow,
                ApplicationVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown",
                DatabaseSchemaVersion = schemaVersion,
                DatabaseFileName = "cafeteria.db",
                BackupType = typeName,
                FileChecksums = checksums,
            };

            var manifestPath = Path.Combine(tempDir, ManifestName);
            await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);

            using (var zip = ZipFile.Open(packagePath, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(dbCopyPath, "cafeteria.db", CompressionLevel.Optimal);
                zip.CreateEntryFromFile(manifestPath, ManifestName, CompressionLevel.Optimal);

                foreach (var templatePath in templateFiles)
                {
                    var entryName = "Templates/" + Path.GetRelativePath(_paths.TemplateDirectory, templatePath).Replace("\\", "/");
                    zip.CreateEntryFromFile(templatePath, entryName, CompressionLevel.Optimal);
                }
            }

            var fileSize = new FileInfo(packagePath).Length;

            db.BackupRecords.Add(new BackupRecord
            {
                Filename = filename,
                StoredFilename = packagePath,
                FileSize = (int)fileSize,
                BackupType = type.ToString(),
                Status = "completed",
                ChecksumSha256 = Sha256(packagePath),
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);

            return await GetBackupInfoByPathAsync(db, packagePath, cancellationToken)
                ?? new BackupInfo
                {
                    Filename = filename,
                    StoredPath = packagePath,
                    FileSize = fileSize,
                    BackupType = type,
                    Status = "completed",
                    CreatedAt = DateTime.UtcNow,
                };
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch
            {
                // ignore
            }
        }
    }

    private static async Task CreateDatabaseCopyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        if (File.Exists(destinationPath))
            File.Delete(destinationPath);

        using var source = new SqliteConnection($"Data Source=\"{sourcePath}\";Pooling=False");
        await source.OpenAsync(cancellationToken);
        using (var command = source.CreateCommand())
        {
            command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        using var destination = new SqliteConnection($"Data Source=\"{destinationPath}\";Pooling=False");
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination, "main", "main");
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static BackupInfo Map(BackupRecord r) => new()
    {
        Id = r.Id,
        Filename = r.Filename,
        StoredPath = r.StoredFilename,
        FileSize = r.FileSize,
        BackupType = Enum.Parse<BackupType>(r.BackupType),
        Status = r.Status,
        CreatedAt = r.CreatedAt,
    };

    private static async Task<BackupInfo?> GetBackupInfoByPathAsync(CafeteriaDbContext db, string path, CancellationToken cancellationToken)
    {
        var r = await db.BackupRecords.FirstOrDefaultAsync(b => b.StoredFilename == path, cancellationToken);
        return r is null ? null : Map(r);
    }
}
