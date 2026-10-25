using System.Windows;
using Res = NSW.Core.Properties.Resources;

namespace NSW.Core.UI;

public class MessageBoxHelper
{
    public static void ShowInfo(string msg) => MessageBox.Show(msg, Res.Msg_Title_Info, MessageBoxButton.OK, MessageBoxImage.Information);

    public static void ShowWarning(string msg) => MessageBox.Show(msg, Res.Msg_Title_Warning, MessageBoxButton.OK, MessageBoxImage.Warning);

    public static void ShowError(string msg) => MessageBox.Show(msg, Res.Msg_Title_Error, MessageBoxButton.OK, MessageBoxImage.Error);

    public static bool ShowQuestion(string msg) => MessageBox.Show(msg, Res.Msg_Title_Question, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}