using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using KpicCafeteria.Application.Abstractions;
using KpicCafeteria.Application.DataManagement;
using KpicCafeteria.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace KpicCafeteria.Infrastructure.DataManagement;

/// <summary>시스템 복구 서비스 구현.</summary>
public sealed class RestoreService : IRestoreService
{
    private const int SupportedBackupVersion = 1;
    private const string ManifestName = "manifest.json";

    private readonly IAppDataPathProvider _paths;
    private readonly IDbContextFactory<CafeteriaDbContext> _factory;
    private readonly IBackupService _backupService;

    public RestoreService(IAppDataPathProvider paths, IDbContextFactory<CafeteriaDbContext> factory, IBackupService backupService)
    {
        _paths = paths;
        _factory = factory;
        _backupService = backupService;
    }

    public async Task<BackupManifest> ValidateAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(packagePath))
            throw new RestoreException("백업 파일을 찾을 수 없습니다.");

        var tempDir = Path.Combine(_paths.TempDirectory, $"restore-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var manifest = await ReadAndExtractManifestAsync(packagePath, tempDir, cancellationToken);

            var dbPath = Path.Combine(tempDir, manifest.DatabaseFileName);
            if (!File.Exists(dbPath))
                throw new RestoreException($"백업에 {manifest.DatabaseFileName}이(가) 없습니다.");

            var expected = manifest.FileChecksums.GetValueOrDefault(manifest.DatabaseFileName);
            if (!string.IsNullOrEmpty(expected) && !Sha256(dbPath).Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new RestoreException("데이터베이스 체크섬이 일치하지 않습니다.");

            await VerifySqliteAsync(dbPath, cancellationToken);
            return manifest;
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

    public async Task<bool> RestoreAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        var manifest = await ValidateAsync(packagePath, cancellationToken);

        // 1. Pre-restore backup
        await _backupService.CreatePreRestoreBackupAsync(cancellationToken);

        var tempDir = Path.Combine(_paths.TempDirectory, $"restore-staging-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            await ExtractPackageAsync(packagePath, tempDir, cancellationToken);

            // 2. Close all SQLite connections before replacing files
            SqliteConnection.ClearAllPools();

            // 3. Replace database atomically
            var restoredDb = Path.Combine(tempDir, manifest.DatabaseFileName);
            await ReplaceDatabaseAsync(restoredDb, cancellationToken);

            // 4. Restore templates
            var templatesDir = Path.Combine(tempDir, "Templates");
            if (Directory.Exists(templatesDir))
            {
                if (Directory.Exists(_paths.TemplateDirectory))
                {
                    Directory.Delete(_paths.TemplateDirectory, true);
                }
                Directory.CreateDirectory(_paths.TemplateDirectory);
                foreach (var file in Directory.EnumerateFiles(templatesDir, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(templatesDir, file);
                    var target = Path.Combine(_paths.TemplateDirectory, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(file, target, true);
                }
            }

            // 5. Run any pending migrations and verify
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            await db.Database.MigrateAsync(cancellationToken);

            SqliteConnection.ClearAllPools();
            await VerifySqliteAsync(_paths.DatabasePath, cancellationToken);

            return true;
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

    private static async Task<BackupManifest> ReadAndExtractManifestAsync(string packagePath, string tempDir, CancellationToken cancellationToken)
    {
        using var zip = ZipFile.OpenRead(packagePath);
        var manifestEntry = zip.GetEntry(ManifestName)
            ?? throw new RestoreException("manifest.json이 없습니다.");
        var manifestPath = Path.Combine(tempDir, ManifestName);
        manifestEntry.ExtractToFile(manifestPath, true);
        var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
        var manifest = JsonSerializer.Deserialize<BackupManifest>(json)
            ?? throw new RestoreException("manifest.json을 해석할 수 없습니다.");

        if (manifest.BackupVersion != SupportedBackupVersion)
            throw new RestoreException($"지원하지 않는 백업 버전입니다: {manifest.BackupVersion}");

        if (string.IsNullOrEmpty(manifest.DatabaseFileName))
            throw new RestoreException("데이터베이스 파일명이 manifest에 없습니다.");

        var dbEntry = zip.GetEntry(manifest.DatabaseFileName)
            ?? throw new RestoreException($"백업에 {manifest.DatabaseFileName} 항목이 없습니다.");
        dbEntry.ExtractToFile(Path.Combine(tempDir, manifest.DatabaseFileName), true);
        return manifest;
    }

    private static async Task ExtractPackageAsync(string packagePath, string tempDir, CancellationToken cancellationToken)
    {
        await Task.Run(() => ZipFile.ExtractToDirectory(packagePath, tempDir, true), cancellationToken);
    }

    private static async Task VerifySqliteAsync(string dbPath, CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection($"Data Source=\"{dbPath}\";Pooling=False");
        await connection.OpenAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is not "ok")
            throw new RestoreException($"데이터베이스 무결성 검사 실패: {result}");
    }

    private async Task ReplaceDatabaseAsync(string restoredDb, CancellationToken cancellationToken)
    {
        var dataDir = _paths.DataDirectory;
        var liveDb = _paths.DatabasePath;
        var tempOld = Path.Combine(dataDir, "cafeteria.db.pre_restore_old");

        if (File.Exists(tempOld))
            File.Delete(tempOld);

        if (File.Exists(liveDb))
        {
            File.Move(liveDb, tempOld);
        }

        DeleteWalFiles(dataDir);

        try
        {
            File.Copy(restoredDb, liveDb, false);
        }
        catch
        {
            // rollback
            if (File.Exists(tempOld))
            {
                File.Move(tempOld, liveDb, true);
            }
            throw;
        }

        // keep old file for a short grace period; safe to remove now
        try
        {
            if (File.Exists(tempOld))
                File.Delete(tempOld);
        }
        catch
        {
            // ignore
        }
    }

    private static void DeleteWalFiles(string dataDir)
    {
        foreach (var ext in new[] { "-wal", "-shm" })
        {
            var file = Path.Combine(dataDir, $"cafeteria.db{ext}");
            if (File.Exists(file))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // ignore
                }
            }
        }
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
