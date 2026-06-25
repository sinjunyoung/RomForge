using _3DS.Core.Models;
using _3DS.Core.Services;
using NSW.WPF.UI;
using RomForge.Helpers;
using RomForge.ViewModels;
using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
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

        Loaded += async (_, _) =>
        {
            try
            {
                await TestExeFsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"예외: {ex}");
                MessageBox.Show(ex.ToString());
            }
        };
    }

    private static async Task TestExeFsAsync()
    {
        var keyStore = new _3DS.Core.Crypto.KeyStore();
        await using var cciSource = await CciSource.OpenAsync(@"D:\3ds\Ocarina of Time 3D.cci", keyStore);
        var ct = CancellationToken.None;

        var (ncchStream, _) = cciSource.OpenContentDecrypted(0);
        await using (ncchStream)
        {
            var unpack = await RomFsUnpacker.UnpackAsync(ncchStream, cciSource.MainHeader, ct);

            // original 먼저 읽기
            long romfsOffset = (long)cciSource.MainHeader.RomfsOffset * 0x200;
            long romfsSize = (long)cciSource.MainHeader.RomfsSize * 0x200;
            byte[] original = new byte[romfsSize];
            ncchStream.Position = romfsOffset;
            await ncchStream.ReadExactlyAsync(original, ct);
            byte[] repacked = await RomFsPacker.PackAsync(ncchStream, unpack, ct);

            ncchStream.Position = romfsOffset;
            await ncchStream.ReadExactlyAsync(original, ct);            
            
            var origIvfc = IvfcHeader.Parse(original);
            ulong realOff3 = (ulong)origIvfc.GetDataLevel2Offset();            
            int l3Base = (int)realOff3;
            var origRomfs = RomFsHeader.Parse(original, l3Base);
            int dirEntryBase = l3Base + (int)origRomfs.DirEntryOffset;
            ulong level3Size = origIvfc.Levels[2].Size;
            ulong level2Size = origIvfc.Levels[1].Size;
            ulong level1Size = origIvfc.Levels[0].Size;
            ulong realOffLevel1Hash = AlignUp(realOff3 + level3Size, 0x1000UL);
            ulong realOffLevel2Hash = AlignUp(realOffLevel1Hash + level1Size, 0x1000UL);
            static ulong AlignUp(ulong v, ulong a) => (v + a - 1) & ~(a - 1);
            bool ivfcMatch = repacked.AsSpan(0, 0x5C).SequenceEqual(original.AsSpan(0, 0x5C));
            bool level3Match = repacked.AsSpan((int)realOff3, (int)level3Size).SequenceEqual(original.AsSpan((int)realOff3, (int)level3Size));
            bool level2Match = repacked.AsSpan((int)realOffLevel2Hash, (int)level2Size).SequenceEqual(original.AsSpan((int)realOffLevel2Hash, (int)level2Size));
            bool level1Match = repacked.AsSpan((int)realOffLevel1Hash, (int)level1Size).SequenceEqual(original.AsSpan((int)realOffLevel1Hash, (int)level1Size));
            byte[] orig = original;
            byte[] rep = repacked;

            int level2HashBase = 0x1A585000; // pack offLevel2Hash
            int diffBlockIdx = 0x3220 / 0x20; // = 0x191
            int diffBlockOffset = level2HashBase + diffBlockIdx * 0x1000;

            Debug.WriteLine($"Level2해시 {diffBlockIdx}번째 블록 @ 0x{diffBlockOffset:X}");

            bool blockMatch = repacked.AsSpan(diffBlockOffset, 0x1000)
                .SequenceEqual(original.AsSpan(diffBlockOffset, 0x1000));
            Debug.WriteLine($"해당 블록 일치: {(blockMatch ? "OK ✅" : "FAIL ❌")}");

            // Level2해시가 해시하는 Level3 블록
            int level3BlockOffset = (int)realOff3 + diffBlockIdx * 0x1000;
            Debug.WriteLine($"대응 Level3 블록 @ 0x{level3BlockOffset:X}");
            bool level3BlockMatch = repacked.AsSpan(level3BlockOffset, 0x1000)
                .SequenceEqual(original.AsSpan(level3BlockOffset, 0x1000));
            Debug.WriteLine($"Level3 해당 블록 일치: {(level3BlockMatch ? "OK ✅" : "FAIL ❌")}");
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