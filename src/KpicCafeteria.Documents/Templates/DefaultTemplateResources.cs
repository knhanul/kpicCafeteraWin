using System.Reflection;

namespace KpicCafeteria.Documents.Templates;

/// <summary>
/// 임베디드 기본 HWPX 템플릿 자원 접근자.
/// assets/templates/*.hwpx가 EmbeddedResource로 포함된다.
/// </summary>
public static class DefaultTemplateResources
{
    public const string MealPlanResourceName = "KpicCafeteria.Documents.Templates.meal-plan.hwpx";
    public const string CookingInstructionResourceName = "KpicCafeteria.Documents.Templates.cooking-instruction.hwpx";
    public const string PreservationRecordResourceName = "KpicCafeteria.Documents.Templates.preservation-record.hwpx";
    public const string PageConfigResourceName = "KpicCafeteria.Documents.Templates.template-page-config.json";

    private static readonly Assembly Assembly = typeof(DefaultTemplateResources).Assembly;

    /// <summary>문서 유형 → 임베디드 자원 이름.</summary>
    public static string ResourceNameFor(string documentType)
    {
        return documentType switch
        {
            "MEAL_PLAN" => MealPlanResourceName,
            "COOKING_INSTRUCTION" => CookingInstructionResourceName,
            "PRESERVATION_RECORD" => PreservationRecordResourceName,
            _ => throw new ArgumentException($"지원하지 않는 문서 유형입니다: {documentType}", nameof(documentType)),
        };
    }

    /// <summary>임베디드 기본 템플릿 바이트. 없으면 null.</summary>
    public static byte[]? TryGetTemplateBytes(string documentType)
    {
        var name = ResourceNameFor(documentType);
        using var stream = Assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            return null;
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    /// <summary>임베디드 기본 템플릿 바이트. 없으면 예외.</summary>
    public static byte[] GetTemplateBytes(string documentType)
    {
        return TryGetTemplateBytes(documentType)
            ?? throw new InvalidOperationException($"임베디드 기본 템플릿을 찾을 수 없습니다: {documentType}");
    }

    /// <summary>임베디드 기본 템플릿의 SHA-256.</summary>
    public static string Sha256(string documentType)
    {
        var bytes = GetTemplateBytes(documentType);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
    }

    /// <summary>임베디드 기본 템플릿의 크기.</summary>
    public static int Size(string documentType) => GetTemplateBytes(documentType).Length;
}
