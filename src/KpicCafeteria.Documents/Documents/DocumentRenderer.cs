using KpicCafeteria.Documents.Hwpx;

namespace KpicCafeteria.Documents.Documents;

/// <summary>
/// 문서 렌더 파이프라인.
/// 기존 hwpx_engine.py render_document에 대응.
/// </summary>
public static class DocumentRenderer
{
    /// <summary>템플릿 파일 경로로 문서를 렌더링해 HWPX 바이트를 반환한다.</summary>
    public static byte[] Render(string templatePath, string documentType, object payload)
    {
        var engine = HwpxTemplateEngine.LoadTemplate(templatePath);
        return Render(engine, documentType, payload);
    }

    /// <summary>메모리 템플릿 바이트로 문서를 렌더링해 HWPX 바이트를 반환한다.</summary>
    public static byte[] RenderBytes(byte[] templateBytes, string documentType, object payload, string? sourceName = null)
    {
        var engine = HwpxTemplateEngine.LoadTemplateBytes(templateBytes, sourceName);
        return Render(engine, documentType, payload);
    }

    private static byte[] Render(HwpxTemplateEngine engine, string documentType, object payload)
    {
        var renderer = DocumentRendererFactory.For(documentType);
        renderer.Render(engine, payload);
        engine.ValidatePackage(allowRemainingPlaceholders: false);
        return engine.Save(validate: false);
    }
}
