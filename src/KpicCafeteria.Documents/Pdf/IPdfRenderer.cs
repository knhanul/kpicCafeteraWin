namespace KpicCafeteria.Documents.Pdf;

/// <summary>
/// HWPX → PDF 변환기.
/// 기존 hwpx_pdf_renderer.py PdfRenderer에 대응.
/// </summary>
public interface IPdfRenderer
{
    /// <summary>HWPX 바이트를 PDF 바이트로 변환한다.</summary>
    byte[] Render(byte[] hwpxBytes, string sourceName = "document.hwpx");
}

/// <summary>
/// 테스트용 가짜 PDF 렌더러.
/// 실제 한글 변환 없이 파이프라인 검증에 사용한다.
/// </summary>
public sealed class FakePdfRenderer : IPdfRenderer
{
    public byte[] Render(byte[] hwpxBytes, string sourceName = "document.hwpx")
        => System.Text.Encoding.UTF8.GetBytes("%PDF-1.4\nfake-pdf\n");
}
