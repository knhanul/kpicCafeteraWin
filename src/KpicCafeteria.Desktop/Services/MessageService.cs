using System.Windows;

namespace KpicCafeteria.Desktop.Services;

/// <summary>
/// MessageBox 기반 메시지 서비스 구현.
/// </summary>
public sealed class MessageService : IMessageService
{
    public void ShowError(string message)
        => MessageBox.Show(message, "Kpic Cafeteria", MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowInfo(string message)
        => MessageBox.Show(message, "Kpic Cafeteria", MessageBoxButton.OK, MessageBoxImage.Information);

    public bool Confirm(string message, string title = "확인")
        => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
