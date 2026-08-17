namespace KpicCafeteria.Documents.Hwpx;

/// <summary>
/// 템플릿 검증.
/// 기존 hwpx_engine.py validate_template에 대응.
/// </summary>
public static class HwpxTemplateValidator
{
    /// <summary>템플릿 파일 검증. 실패 시 HwpxTemplateError.</summary>
    public static HwpxValidationResult ValidateTemplate(string templatePath, string? documentType = null)
    {
        var package = HwpxPackage.Load(templatePath);
        return package.Validate(documentType, allowRemainingPlaceholders: true);
    }

    /// <summary>메모리 바이트 검증. 실패 시 HwpxTemplateError.</summary>
    public static HwpxValidationResult ValidateTemplateBytes(byte[] content, string? documentType = null, string? sourceName = null)
    {
        var package = HwpxPackage.LoadBytes(content, sourceName);
        return package.Validate(documentType, allowRemainingPlaceholders: true);
    }
}
