using _3DS.Core.Services;
using NSW.WPF.UI;
using RomForge.Helpers;
using RomForge.ViewModels;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;

namespace RomForge.Views;

public partial class MainWindow : Window
{

    private MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
        Closing += MainWindow_Closing;

        _ = TestExeFsAsync();
    }

    private static async Task TestExeFsAsync()
    {
        var keyStore = new _3DS.Core.Crypto.KeyStore();
        await using var cciSource = await CciSource.OpenAsync(@"D:\3ds\Super Mario 3D Land.cci", keyStore);
        var ct = CancellationToken.None;

        var (ncchStream, _) = cciSource.OpenContentDecrypted(0);
        await using (ncchStream)
        {
            // 언팩
            var result = await ExeFsUnpacker.UnpackAsync(ncchStream, cciSource.MainHeader, ct);

            // 재패킹
            byte[] repacked = ExeFsPacker.Pack(result.Files);

            // 원본 읽기
            long exefsOffset = (long)cciSource.MainHeader.ExefsOffset * 0x200;
            long exefsSize = (long)cciSource.MainHeader.ExefsSize * 0x200;
            byte[] original = new byte[exefsSize];
            ncchStream.Position = exefsOffset;
            await ncchStream.ReadExactlyAsync(original, ct);

            // 비교 (재패킹은 실제 데이터 크기만큼만 비교)
            bool match = repacked.AsSpan().SequenceEqual(original.AsSpan(0, repacked.Length));
            Debug.WriteLine($"ExeFS 재패킹 검증: {(match ? "OK ✅" : "FAIL ❌")}");
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        IntPtr hWnd = new WindowInteropHelper(this).Handle;
        int value = 1;

        _ = Win32API.DwmSetWindowAttribute(hWnd, 20, ref value, sizeof(int));
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        ViewModel.SaveConfig();
        bool busy = ViewModel.CompressVM.IsLocked || ViewModel.PatchVM.IsLocked;

        if (!busy)
            return;

        var result = MessageBoxHelper.ShowQuestion("작업이 진행 중입니다. 취소하고 종료할까요?");

        if (result)
        {
            ViewModel.CompressVM.CancelCommand.Execute(null);
            ViewModel.PatchVM.CancelCommand.Execute(null);
        }
        else
            e.Cancel = true;
    }
}