using KpicCafeteria.Application.Abstractions.Repositories;
using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Documents.Documents;
using KpicCafeteria.Documents.Hwpx;
using KpicCafeteria.Documents.Pdf;

namespace KpicCafeteria.Application.Documents;

/// <summary>
/// 문서 출력 서비스.
/// 기존 document_hwpx.py generate_hwpx_bytes/generate_pdf_bytes + documents.py 출력 규칙에 대응.
/// HWPX가 원본 문서이며 PDF는 HWPX에서 생성된다.
/// </summary>
public sealed class DocumentService
{
    private readonly IMealServiceRepositoryFactory _mealServiceFactory;
    private readonly IDocumentTemplateRepositoryFactory _templateFactory;
    private readonly IPdfRenderer _pdfRenderer;

    public DocumentService(
        IMealServiceRepositoryFactory mealServiceFactory,
        IDocumentTemplateRepositoryFactory templateFactory,
        IPdfRenderer pdfRenderer)
    {
        _mealServiceFactory = mealServiceFactory;
        _templateFactory = templateFactory;
        _pdfRenderer = pdfRenderer;
    }

    /// <summary>문서 유형별 활성 템플릿 경로. 없으면 예외.</summary>
    public async Task<string> GetActiveTemplatePathAsync(string documentType, CancellationToken cancellationToken = default)
    {
        if (!DocumentTemplateService.ValidDocumentTypes.Contains(documentType))
        {
            throw new UnsupportedDocumentTypeException(documentType);
        }

        using var repository = _templateFactory.Create();
        var template = await repository.FindActiveAsync(documentType, cancellationToken);
        if (template is null)
        {
            throw new ActiveTemplateNotFoundException(documentType);
        }

        if (!File.Exists(template.StoragePath))
        {
            throw new DocumentException($"{DocumentTypeNames.Get(documentType)} 양식 파일이 없습니다. 문서 양식 메뉴에서 양식을 다시 등록해 주세요.");
        }

        return template.StoragePath;
    }

    /// <summary>기간 내 배식 조회 (메뉴/재료/보존식 포함).</summary>
    public async Task<List<MealService>> ResolveServicesAsync(
        IReadOnlyList<int>? serviceIds,
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        using var repository = _mealServiceFactory.Create();
        if (serviceIds is { Count: > 0 })
        {
            return await repository.GetServicesByIdsAsync(serviceIds, cancellationToken);
        }

        if (startDate is { } start && endDate is { } end)
        {
            return await repository.GetServicesWithDetailsInRangeAsync(start, end, cancellationToken);
        }

        return [];
    }

    /// <summary>문서 DTO 생성.</summary>
    public static object BuildDto(string documentType, IReadOnlyList<MealService> services, DateOnly? startDate = null, DateOnly? endDate = null)
    {
        if (services.Count == 0)
        {
            throw new NoServicesException();
        }

        return documentType switch
        {
            "MEAL_PLAN" => MealPlanDocumentBuilder.Build(services, startDate, endDate),
            "COOKING_INSTRUCTION" => CookingInstructionDocumentBuilder.Build(services, startDate, endDate),
            "PRESERVATION_RECORD" => PreservationRecordDocumentBuilder.Build(services, startDate, endDate),
            _ => throw new UnsupportedDocumentTypeException(documentType),
        };
    }

    /// <summary>문서 DTO → 렌더러 페이로드.</summary>
    public static object BuildPayload(string documentType, object dto)
    {
        return documentType switch
        {
            "MEAL_PLAN" => DocumentPayloadBuilder.ToMealPlanPayload((MealPlanDocumentDto)dto),
            "COOKING_INSTRUCTION" => DocumentPayloadBuilder.ToCookingPayload((CookingInstructionDocumentDto)dto),
            "PRESERVATION_RECORD" => DocumentPayloadBuilder.ToPreservationPayload((PreservationRecordDocumentDto)dto),
            _ => throw new UnsupportedDocumentTypeException(documentType),
        };
    }

    /// <summary>HWPX 생성. (바이트, 파일명) 반환.</summary>
    public async Task<(byte[] Content, string Filename)> GenerateHwpxAsync(
        string documentType,
        IReadOnlyList<int>? serviceIds = null,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var services = await ResolveServicesAsync(serviceIds, startDate, endDate, cancellationToken);
        return await GenerateHwpxAsync(documentType, services, startDate, endDate, cancellationToken);
    }

    /// <summary>이미 조회된 배식 목록으로 HWPX 생성. (바이트, 파일명) 반환.</summary>
    public async Task<(byte[] Content, string Filename)> GenerateHwpxAsync(
        string documentType,
        IReadOnlyList<MealService> services,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        if (services.Count == 0)
        {
            throw new NoServicesException();
        }

        var templatePath = await GetActiveTemplatePathAsync(documentType, cancellationToken);
        var dto = BuildDto(documentType, services, startDate, endDate);
        var payload = BuildPayload(documentType, dto);
        var content = DocumentRenderer.Render(templatePath, documentType, payload);
        var filename = DocumentFilename.ForDto(dto);
        return (content, filename);
    }

    /// <summary>PDF 생성 (HWPX → PDF). (바이트, 파일명) 반환.</summary>
    public async Task<(byte[] Content, string Filename)> GeneratePdfAsync(
        string documentType,
        IReadOnlyList<int>? serviceIds = null,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var services = await ResolveServicesAsync(serviceIds, startDate, endDate, cancellationToken);
        return await GeneratePdfAsync(documentType, services, startDate, endDate, cancellationToken);
    }

    /// <summary>이미 조회된 배식 목록으로 PDF 생성 (HWPX → PDF). (바이트, 파일명) 반환.</summary>
    public async Task<(byte[] Content, string Filename)> GeneratePdfAsync(
        string documentType,
        IReadOnlyList<MealService> services,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var (hwpxBytes, hwpxFilename) = await GenerateHwpxAsync(documentType, services, startDate, endDate, cancellationToken);
        var pdfBytes = _pdfRenderer.Render(hwpxBytes, hwpxFilename);
        return (pdfBytes, hwpxFilename[..^5] + ".pdf");
    }

    /// <summary>
    /// 출력 기록. 실제 저장/출력 성공 시에만 호출한다 (미리보기에서는 기록하지 않음).
    /// 조리지시서 → CookingOutputAt, 식단표 → MealPlanOutputAt.
    /// </summary>
    public async Task MarkOutputAsync(string documentType, IReadOnlyList<MealService> services, CancellationToken cancellationToken = default)
    {
        using var repository = _mealServiceFactory.Create();
        var now = DateTime.UtcNow;
        foreach (var service in services)
        {
            var row = await repository.GetServiceAsync(service.Id, cancellationToken);
            if (row is null)
            {
                continue;
            }

            if (documentType == "COOKING_INSTRUCTION")
            {
                row.CookingOutputAt = now;
            }
            else if (documentType == "MEAL_PLAN")
            {
                row.MealPlanOutputAt = now;
            }
        }

        await repository.SaveChangesAsync(cancellationToken);
    }
}
