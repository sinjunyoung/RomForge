using System.Windows;

namespace RomForge.Core;

public class MessageBoxHelper
{
    public static void ShowInfo(string msg) => MessageBox.Show(msg, "알림", MessageBoxButton.OK, MessageBoxImage.Information);

    public static void ShowWarning(string msg) => MessageBox.Show(msg, "경고", MessageBoxButton.OK, MessageBoxImage.Warning);

    public static void ShowError(string msg) => MessageBox.Show(msg, "오류", MessageBoxButton.OK, MessageBoxImage.Error);

    public static bool ShowQuestion(string msg) => MessageBox.Show(msg, "확인", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}