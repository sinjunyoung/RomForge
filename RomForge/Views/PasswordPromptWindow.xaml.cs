using Common;
using Patch.Core.Interfaces;
using Patch.Core.Models;
using Patch.Core.Services;
using RomForge.Core.UI.Helpers;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace RomForge.Views;

public partial class PasswordPromptWindow : Window
{
    public string? Password { get; private set; }

    public PasswordPromptWindow(string archivePath, bool wrongPassword = false)
    {
        InitializeComponent();

        txtFileName.Text = Path.GetFileName(archivePath);

        if (wrongPassword)
        {
            txtMessage.Text = "비밀번호가 올바르지 않습니다. 다시 입력해 주세요.";
            txtError.Visibility = Visibility.Visible;
        }

        Loaded += (_, _) => pwBox.Focus();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        IntPtr hWnd = new WindowInteropHelper(this).Handle;
        int value = 1;

        _ = Win32API.DwmSetWindowAttribute(hWnd, 20, ref value, sizeof(int));
    }

    private void PwBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            Accept();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e) => Accept();

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;

        Close();
    }

    private void Accept()
    {
        Password = pwBox.Text;
        DialogResult = true;

        Close();
    }

    public static async Task<(IArchivePatchSource Archive, string? Password)?> OpenWithPasswordPromptAsync(string archivePath, Window? owner)
    {
        bool wrongPassword = false;
        string? password = null;

        while (true)
        {
            try
            {
                string? attemptedPassword = password;
                var archive = await Task.Run(() => ArchivePatchSourceFactory.Open(archivePath, attemptedPassword));

                return (archive, password);
            }
            catch (ArchivePasswordRequiredException)
            {
                var dlg = new PasswordPromptWindow(archivePath, wrongPassword);

                if (owner != null)
                    dlg.Owner = owner;

                if (dlg.ShowDialog() != true)
                    return null;

                password = dlg.Password;
                wrongPassword = true;
            }
        }
    }
}