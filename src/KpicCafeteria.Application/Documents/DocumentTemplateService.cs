using KpicCafeteria.Application.Abstractions;
using KpicCafeteria.Application.Abstractions.Repositories;
using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Documents.Hwpx;
using KpicCafeteria.Documents.Templates;

namespace KpicCafeteria.Application.Documents;

/// <summary>
/// HWPX 문서 템플릿 관리 서비스.
/// 기존 templates.py / master_data.py의 템플릿 관리 규칙을 유지한다.
/// </summary>
public sealed class DocumentTemplateService
{
    public static readonly string[] ValidDocumentTypes = ["MEAL_PLAN", "COOKING_INSTRUCTION", "PRESERVATION_RECORD"];

    private readonly IDocumentTemplateRepositoryFactory _factory;
    private readonly IAppDataPathProvider _paths;

    public DocumentTemplateService(IDocumentTemplateRepositoryFactory factory, IAppDataPathProvider paths)
    {
        _factory = factory;
        _paths = paths;
    }

    private IDocumentTemplateRepository CreateRepository() => _factory.Create();

    public static bool IsValidDocumentType(string documentType)
        => ValidDocumentTypes.Contains(documentType);

    // =======================================================================
    // 조회
    // =======================================================================

    public async Task<List<DocumentTemplate>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        return await repository.ListAsync(cancellationToken);
    }

    public async Task<DocumentTemplate?> FindActiveAsync(string documentType, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        return await repository.FindActiveAsync(documentType, cancellationToken);
    }

    // =======================================================================
    // 등록/활성화/삭제
    // =======================================================================

    /// <summary>템플릿 등록. 검증 실패 시 HwpxTemplateError.</summary>
    public async Task<DocumentTemplate> RegisterAsync(
        string documentType,
        string name,
        byte[] fileContent,
        string originalFilename,
        bool activate,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidDocumentType(documentType))
        {
            throw new UnsupportedDocumentTypeException(documentType);
        }

        if (fileContent.Length == 0)
        {
            throw new DocumentException("빈 파일은 등록할 수 없습니다.");
        }

        // 검증 먼저 수행 (실패 시 파일 저장 안 함)
        var validation = HwpxTemplateValidator.ValidateTemplateBytes(fileContent, documentType, originalFilename);

        var folder = Path.Combine(_paths.TemplateDirectory, documentType.ToLowerInvariant());
        Directory.CreateDirectory(folder);
        var storedFilename = $"{Guid.NewGuid():N}.hwpx";
        var storagePath = Path.Combine(folder, storedFilename);
        await File.WriteAllBytesAsync(storagePath, fileContent, cancellationToken);

        using var repository = CreateRepository();
        var version = (await repository.MaxVersionAsync(documentType, cancellationToken)) + 1;
        if (activate)
        {
            foreach (var existing in await repository.ListByTypeAsync(documentType, cancellationToken))
            {
                existing.Active = false;
            }
        }

        var template = new DocumentTemplate
        {
            DocumentType = documentType,
            Name = name.Trim(),
            OriginalFilename = originalFilename,
            StoredFilename = storedFilename,
            StoragePath = storagePath,
            FileSize = fileContent.Length,
            ChecksumSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(fileContent)).ToLowerInvariant(),
            Active = activate,
            Version = version,
            IsValid = true,
            ValidationMessage = null,
            PlaceholderSummary = new Dictionary<string, object?>
            {
                ["sections"] = validation.Sections,
                ["placeholders"] = validation.Placeholders.ToList(),
            },
            CreatedAt = DateTime.UtcNow,
        };
        repository.Add(template);
        await repository.SaveChangesAsync(cancellationToken);
        return template;
    }

    /// <summary>템플릿 활성화. 파일 검증 후 이전 활성 템플릿을 비활성화한다.</summary>
    public async Task ActivateAsync(int templateId, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var template = await repository.GetAsync(templateId, cancellationToken)
            ?? throw new TemplateNotFoundException(templateId);

        // 활성화 전 파일 재검증 (손상 시 활성화 불가)
        if (!File.Exists(template.StoragePath))
        {
            throw new DocumentException("템플릿 파일이 없어 활성화할 수 없습니다.");
        }

        HwpxTemplateValidator.ValidateTemplate(template.StoragePath, template.DocumentType);

        foreach (var existing in await repository.ListByTypeAsync(template.DocumentType, cancellationToken))
        {
            existing.Active = false;
        }

        template.Active = true;
        template.IsValid = true;
        template.ValidationMessage = null;
        await repository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>템플릿 삭제. 활성 템플릿은 삭제 불가.</summary>
    public async Task DeleteAsync(int templateId, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var template = await repository.GetAsync(templateId, cancellationToken)
            ?? throw new TemplateNotFoundException(templateId);
        if (template.Active)
        {
            throw new ActiveTemplateDeleteException();
        }

        repository.Remove(template);
        await repository.SaveChangesAsync(cancellationToken);

        try
        {
            if (File.Exists(template.StoragePath))
            {
                File.Delete(template.StoragePath);
            }
        }
        catch (IOException)
        {
            // 파일 삭제 실패는 치명적이지 않다.
        }
    }

    // =======================================================================
    // 기본 양식
    // =======================================================================

    /// <summary>
    /// 최초 실행 시 문서 유형별 활성 템플릿이 없으면 임베디드 기본 양식을 등록한다.
    /// </summary>
    public async Task SeedDefaultsAsync(CancellationToken cancellationToken = default)
    {
        foreach (var documentType in ValidDocumentTypes)
        {
            using (var repository = CreateRepository())
            {
                var active = await repository.FindActiveAsync(documentType, cancellationToken);
                if (active is not null)
                {
                    continue;
                }
            }

            var bytes = DefaultTemplateResources.TryGetTemplateBytes(documentType);
            if (bytes is null)
            {
                continue;
            }

            await RegisterAsync(
                documentType,
                $"{DocumentTypeNames.Get(documentType)} 기본 양식",
                bytes,
                $"{documentType.ToLowerInvariant()}-default.hwpx",
                activate: true,
                cancellationToken);
        }
    }

    /// <summary>
    /// 기본 양식 복원. 임베디드 기본 양식을 새 버전으로 등록한다.
    /// 기존 사용자 양식/파일은 삭제하지 않는다.
    /// </summary>
    public async Task<DocumentTemplate> RestoreDefaultAsync(string documentType, CancellationToken cancellationToken = default)
    {
        if (!IsValidDocumentType(documentType))
        {
            throw new UnsupportedDocumentTypeException(documentType);
        }

        var bytes = DefaultTemplateResources.TryGetTemplateBytes(documentType)
            ?? throw new DocumentException("기본 양식 파일을 찾을 수 없습니다.");

        return await RegisterAsync(
            documentType,
            $"{DocumentTypeNames.Get(documentType)} 기본 양식",
            bytes,
            $"{documentType.ToLowerInvariant()}-default.hwpx",
            activate: true,
            cancellationToken);
    }
}
