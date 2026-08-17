using KpicCafeteria.Domain.Common;
using KpicCafeteria.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KpicCafeteria.Infrastructure.Persistence;

/// <summary>
/// KpicCafeteria SQLite DbContext.
/// 테이블명/컬럼명은 기존 Python DB의 snake_case 명칭을 유지한다.
/// </summary>
public sealed class CafeteriaDbContext : DbContext
{
    public CafeteriaDbContext(DbContextOptions<CafeteriaDbContext> options)
        : base(options)
    {
    }

    public DbSet<MealTypeSetting> MealTypeSettings => Set<MealTypeSetting>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<IngredientAlias> IngredientAliases => Set<IngredientAlias>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<MealService> MealServices => Set<MealService>();
    public DbSet<MealServiceMenu> MealServiceMenus => Set<MealServiceMenu>();
    public DbSet<MealServiceMenuIngredient> MealServiceMenuIngredients => Set<MealServiceMenuIngredient>();
    public DbSet<PreservationRecord> PreservationRecords => Set<PreservationRecord>();
    public DbSet<MealActual> MealActuals => Set<MealActual>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderGroup> OrderGroups => Set<OrderGroup>();
    public DbSet<DocumentTemplate> DocumentTemplates => Set<DocumentTemplate>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<BackupRecord> BackupRecords => Set<BackupRecord>();
    public DbSet<DataArchive> DataArchives => Set<DataArchive>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CafeteriaDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // 시스템 Timestamp는 UTC로 저장한다. (WPF 표시 시 Local Time으로 변환)
        configurationBuilder.Properties<DateTime>().HaveConversion<ValueConverters.UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<ValueConverters.NullableUtcDateTimeConverter>();
        base.ConfigureConventions(configurationBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// CreatedAt/UpdatedAt 자동 기록 (기존 models.py의 default=utcnow / onupdate=utcnow 대응).
    /// </summary>
    private void ApplyTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added && entry.Entity is IHasCreatedAt created)
            {
                created.CreatedAt = now;
            }

            if (entry.State is EntityState.Added or EntityState.Modified && entry.Entity is IHasUpdatedAt updated)
            {
                updated.UpdatedAt = now;
            }
        }
    }
}
