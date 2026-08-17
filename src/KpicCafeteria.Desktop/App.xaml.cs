using System.IO;
using System.Windows;
using System.Threading.Tasks;
using KpicCafeteria.Application.Abstractions;
using KpicCafeteria.Application.Abstractions.Repositories;
using KpicCafeteria.Application.DataManagement;
using KpicCafeteria.Application.Documents;
using KpicCafeteria.Application.MasterData;
using KpicCafeteria.Application.Orders;
using KpicCafeteria.Application.Statistics;
using KpicCafeteria.Application.Workspace;
using KpicCafeteria.Infrastructure.DataManagement;
using KpicCafeteria.Documents.Pdf;
using KpicCafeteria.Desktop.Services;
using KpicCafeteria.Desktop.ViewModels;
using KpicCafeteria.Desktop.ViewModels.Statistics;
using KpicCafeteria.Desktop.Views;
using KpicCafeteria.Desktop.Views.Statistics;
using KpicCafeteria.Infrastructure;
using KpicCafeteria.Infrastructure.Persistence;
using KpicCafeteria.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KpicCafeteria.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// 앱 시작 흐름:
/// DI Build → App Data Directory 확인 → SQLite DB 초기화 → EF Migration → Default Seed → MainWindow 표시
/// </summary>
public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;
    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KpicCafeteria", "app.log");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _services = BuildServices();

        // App Data Directory 확인 (경로 접근 시 디렉터리 자동 생성)
        var paths = _services.GetRequiredService<IAppDataPathProvider>();
        _ = paths.DatabasePath;

        // SQLite DB 초기화 (WAL/busy_timeout 설정 → Migration → Seed)
        var initializer = _services.GetRequiredService<IDatabaseInitializer>();
        await initializer.InitializeAsync();

        // 문서 양식: 유형별 활성 양식이 없으면 임베디드 기본 양식 등록
        await _services.GetRequiredService<DocumentTemplateService>()
            .SeedDefaultsAsync();

        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash(e.Exception);
        MessageBox.Show($"예상치 못한 오류가 발생했습니다.\n\n{e.Exception.Message}\n\n로그: {LogFile}",
            "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            LogCrash(ex);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogCrash(e.Exception);
        e.SetObserved();
    }

    private static void LogCrash(Exception ex)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogFile);
            if (dir is not null) Directory.CreateDirectory(dir);
            File.AppendAllText(LogFile,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] CRASH: {ex}\n\n");
        }
        catch { }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.AddProvider(new FileLoggerProvider(LogFile));
        });

        services.AddSingleton<IAppDataPathProvider, AppDataPathProvider>();

        services.AddDbContextFactory<CafeteriaDbContext>((provider, options) =>
        {
            var paths = provider.GetRequiredService<IAppDataPathProvider>();
            options.UseSqlite($"Data Source={paths.DatabasePath};Foreign Keys=True", sqlite =>
            {
                sqlite.CommandTimeout(30);
            });
        });

        services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();

        // 기준정보 (Master Data)
        services.AddSingleton<IMasterDataRepositoryFactory, MasterDataRepositoryFactory>();
        services.AddSingleton<MasterDataService>();
        services.AddSingleton<IMessageService, MessageService>();
        services.AddSingleton<MenuRecipeViewModel>();
        services.AddSingleton<IngredientViewModel>();
        services.AddSingleton<MealDefaultsViewModel>();
        services.AddSingleton<MenuRecipeView>();
        services.AddSingleton<IngredientView>();
        services.AddSingleton<MealDefaultsView>();

        // 주간 급식 운영 (Workspace)
        services.AddSingleton<IMealServiceRepositoryFactory, MealServiceRepositoryFactory>();
        services.AddSingleton<WorkspaceService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<WorkspaceViewModel>();
        services.AddSingleton<WorkspaceView>();

        // 발주 관리 (Orders)
        services.AddSingleton<IOrderRepositoryFactory, OrderRepositoryFactory>();
        services.AddSingleton<OrderService>();
        services.AddSingleton<OrdersViewModel>();
        services.AddSingleton<OrdersView>();

        // 문서 출력 (Documents)
        services.AddSingleton<IDocumentTemplateRepositoryFactory, DocumentTemplateRepositoryFactory>();
        services.AddSingleton<DocumentTemplateService>();
        services.AddSingleton<IPdfRenderer, HancomPdfRenderer>();
        services.AddSingleton<DocumentService>();
        services.AddSingleton<ExcelExportService>();
        services.AddSingleton<DocumentOutputViewModel>();
        services.AddSingleton<DocumentOutputView>();
        services.AddSingleton<DocumentTemplatesViewModel>();
        services.AddSingleton<DocumentTemplatesView>();

        // 통계 (Statistics)
        services.AddSingleton<IStatisticsRepositoryFactory, StatisticsRepositoryFactory>();
        services.AddSingleton<MealStatisticsService>();
        services.AddSingleton<MenuStatisticsService>();
        services.AddSingleton<IngredientStatisticsService>();
        services.AddSingleton<OperationsStatisticsService>();
        services.AddSingleton<DashboardService>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<MealStatisticsViewModel>();
        services.AddSingleton<MenuStatisticsViewModel>();
        services.AddSingleton<IngredientStatisticsViewModel>();
        services.AddSingleton<OperationsStatisticsViewModel>();
        services.AddSingleton<DashboardView>();
        services.AddSingleton<MealStatisticsView>();
        services.AddSingleton<MenuStatisticsView>();
        services.AddSingleton<IngredientStatisticsView>();
        services.AddSingleton<OperationsStatisticsView>();

        // 데이터 관리 (Import/Backup/Restore/Archive)
        services.AddSingleton<IImportService, ImportService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IRestoreService, RestoreService>();
        services.AddSingleton<DataManagementViewModel>();
        services.AddSingleton<DataManagementView>();

        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}

