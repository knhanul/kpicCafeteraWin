namespace KpicCafeteria.Documents.Hwpx;

/// <summary>
/// HWPX 템플릿/패키지 오류.
/// 기존 hwpx_engine.py HwpxTemplateError에 대응.
/// </summary>
public class HwpxTemplateError : Exception
{
    public HwpxTemplateError(string message)
        : base(message)
    {
    }

    public HwpxTemplateError(string message, Exception inner)
        : base(message, inner)
    {
    }
}
