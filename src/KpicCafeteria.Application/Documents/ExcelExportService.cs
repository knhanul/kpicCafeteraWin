using KpicCafeteria.Application.Abstractions;
using KpicCafeteria.Application.Abstractions.Repositories;
using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Documents.Excel;

namespace KpicCafeteria.Application.Documents;

/// <summary>
/// Excel 데이터 아카이브 생성 서비스.
/// 기존 admin.py _build_excel + 데이터 아카이브 다운로드 규칙에 대응.
/// </summary>
public sealed class ExcelExportService
{
    private readonly IMealServiceRepositoryFactory _mealServiceFactory;
    private readonly IMasterDataRepositoryFactory _masterDataFactory;
    private readonly IOrderRepositoryFactory _orderFactory;
    private readonly IAppDataPathProvider _paths;

    public ExcelExportService(
        IMealServiceRepositoryFactory mealServiceFactory,
        IMasterDataRepositoryFactory masterDataFactory,
        IOrderRepositoryFactory orderFactory,
        IAppDataPathProvider paths)
    {
        _mealServiceFactory = mealServiceFactory;
        _masterDataFactory = masterDataFactory;
        _orderFactory = orderFactory;
        _paths = paths;
    }

    /// <summary>기간 내 데이터 아카이브 Excel 생성. (바이트, 파일명) 반환.</summary>
    public async Task<(byte[] Content, string Filename)> ExportAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
        {
            throw new DocumentException("시작일이 종료일보다 늦습니다.");
        }

        using (var mealRepository = _mealServiceFactory.Create())
        {
            var services = await mealRepository.GetServicesWithDetailsInRangeAsync(startDate, endDate, cancellationToken);
            using var masterRepository = _masterDataFactory.Create();
            var menus = await masterRepository.GetAllMenusAsync(cancellationToken);
            var ingredients = await masterRepository.GetAllIngredientsAsync(cancellationToken);
            var recipes = await masterRepository.GetAllRecipesWithDetailsAsync(cancellationToken);
            var mealTypes = await masterRepository.GetMealTypeSettingsAsync(cancellationToken);

            using var orderRepository = _orderFactory.Create();
            var orderItems = await orderRepository.GetItemsInRangeAsync(startDate, endDate, cancellationToken);
            var orderGroups = orderItems.Select(i => i.OrderGroup).OfType<OrderGroup>().Distinct().ToList();

            var content = ExcelArchiveExporter.Build(services, menus, ingredients, recipes, mealTypes, orderItems, orderGroups);
            var filename = $"데이터아카이브_{startDate:yyyyMMdd}-{endDate:yyyyMMdd}.xlsx";
            return (content, filename);
        }
    }

    /// <summary>아카이브 디렉터리에 저장하고 저장 경로를 반환한다.</summary>
    public async Task<string> SaveToArchiveAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var (content, filename) = await ExportAsync(startDate, endDate, cancellationToken);
        var path = Path.Combine(_paths.ArchiveDirectory, filename);
        await File.WriteAllBytesAsync(path, content, cancellationToken);
        return path;
    }
}
