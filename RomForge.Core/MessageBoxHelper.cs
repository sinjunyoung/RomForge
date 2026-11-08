using System.Windows;
using Common.Localization;

namespace RomForge.Core;

public class MessageBoxHelper
{
    public static void ShowInfo(string msg) => MessageBox.Show(msg, Loc.S.MsgTitleInfo, MessageBoxButton.OK, MessageBoxImage.Information);

    public static void ShowWarning(string msg) => MessageBox.Show(msg, Loc.S.MsgTitleWarning, MessageBoxButton.OK, MessageBoxImage.Warning);

    public static void ShowError(string msg) => MessageBox.Show(msg, Loc.S.MsgTitleError, MessageBoxButton.OK, MessageBoxImage.Error);

    public static bool ShowQuestion(string msg) => MessageBox.Show(msg, Loc.S.MsgTitleQuestion, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}