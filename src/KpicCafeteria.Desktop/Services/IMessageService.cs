namespace KpicCafeteria.Desktop.Services;

/// <summary>
/// 사용자 메시지 표시 서비스 (ViewModel에서 MessageBox 직접 사용 금지).
/// </summary>
public interface IMessageService
{
    void ShowError(string message);

    void ShowInfo(string message);

    /// <summary>확인 대화상자. true면 사용자가 확인을 선택.</summary>
    bool Confirm(string message, string title = "확인");
}
