using KpicCafeteria.Application.DataManagement;
using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KpicCafeteria.Infrastructure.DataManagement;

/// <summary>데이터 이관 서비스 구현.</summary>
public sealed class ImportService : IImportService
{
    private readonly IDbContextFactory<CafeteriaDbContext> _factory;

    public ImportService(IDbContextFactory<CafeteriaDbContext> factory)
    {
        _factory = factory;
    }

    public Task<ImportPreview> PreviewAsync(string filePath, CancellationToken cancellationToken = default)
        => new MigrationImporter(filePath).PreviewAsync(cancellationToken);

    public async Task<ImportApplyResult> ApplyAsync(
        string filePath,
        ImportMode mode,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var preview = await new MigrationImporter(filePath).PreviewAsync(cancellationToken);
        if (!preview.Ready)
            throw new ImportException("검증에 실패한 파일은 적용할 수 없습니다.");

        var job = new ImportJob
        {
            Token = Guid.NewGuid().ToString("N"),
            Filename = Path.GetFileName(filePath),
            StoragePath = filePath,
            Status = "PREVIEWED",
            Summary = new Dictionary<string, object?>
            {
                ["sheets"] = preview.SheetCounts,
                ["mode"] = mode.ToString(),
            },
        };
        db.ImportJobs.Add(job);

        try
        {
            var importer = new MigrationImporter(filePath);
            var result = await importer.ApplyAsync(db, mode, cancellationToken);

            job.Status = "COMPLETED";
            job.CompletedAt = DateTime.UtcNow;
            job.Summary["result"] = new Dictionary<string, object?>
            {
                ["meal_types"] = result.MealTypes,
                ["menus"] = result.Menus,
                ["ingredients"] = result.Ingredients,
                ["aliases"] = result.Aliases,
                ["recipes"] = result.Recipes,
                ["services"] = result.Services,
                ["meal_history_rows"] = result.MealHistoryRows,
                ["meal_ingredient_rows"] = result.MealIngredientRows,
            };

            db.AuditLogs.Add(new AuditLog
            {
                Action = "MIGRATION_IMPORT",
                EntityType = "WORKBOOK",
                EntityId = Path.GetFileName(filePath),
                Detail = job.Summary,
            });

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            job.Status = "FAILED";
            job.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }
}
