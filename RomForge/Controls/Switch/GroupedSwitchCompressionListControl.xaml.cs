using Common.Models;
using NSW.Core;
using NSW.Core.Models;
using RomForge.Converters;
using RomForge.Core;
using RomForge.Core.Models.Switch;
using RomForge.Core.Services.Switch;
using RomForge.Core.ViewModels.Switch;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace RomForge.Controls.Switch;

public partial class GroupedSwitchCompressionListControl : CommonListControlBase
{
    private readonly ListViewColumnSorter _sorter = new();

    private readonly SemaphoreSlim _estimateGate = new(1, 1);

    public Button ExtraButton1 => btnExtra1;

    public Action? ExtraButton1Clicked;

    public Button ExtraButton2 => btnExtra2;

    public Action? ExtraButton2Clicked;

    public event Action<string, LogLevel>? LogRequested;

    protected override ListView? TargetListView => lvFiles;

    protected override FrameworkElement? DropHintElement => dropHint;

    public GroupedSwitchCompressionListControl()
    {
        InitializeComponent();

        if (CollectionViewSource.GetDefaultView(GameFiles) is ListCollectionView view)
        {
            view.GroupDescriptions.Clear();
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(GameFile.TitleID), new TitleIdGroupConverter()));

            view.CustomSort = new SwitchItemSortComparer();
        }

        lvFiles.ItemsSource = GameFiles;

        UpdateDropHint();
    }

    public static bool KeyExists() => KeySetProvider.Instance.KeySet != null;

    public void RecalcKeyMissingFiles(Action onCompleted) => _ = RecalcKeyMissingFilesAsync(onCompleted);

    private async Task RecalcKeyMissingFilesAsync(Action onCompleted)
    {
        try
        {
            var keySet = KeySetProvider.Instance.KeySet;
            if (keySet == null) return;

            var targets = GameFiles.Where(f => f.IsKeyMissing).ToList();
            var accepted = new List<GameFile>();

            foreach (var vm in targets)
            {
                if (await Task.Run(() => Lz4RomSources.HasSparseLayer(vm.FilePath)))
                {
                    GameFiles.Remove(vm);
                    LogRequested?.Invoke(SparseMessage(vm.FilePath), LogLevel.Warning);
                    continue;
                }

                var info = await Task.Run<GameFileInfo?>(() =>
                {
                    try
                    {
                        return MetadataReader.ReadGameFileInfo(keySet.Clone(), vm.FilePath);
                    }
                    catch
                    {
                        return null;
                    }
                });

                ApplyInfo(vm, info);
                accepted.Add(vm);
            }

            UpdateDropHint();
            CollectionViewSource.GetDefaultView(GameFiles)?.Refresh();
            _ = EstimateAsync(accepted);
        }
        finally
        {
            onCompleted();
        }
    }

    protected override async Task AddPathsAsync(IEnumerable<string> paths)
    {
        var existing = GameFiles.ToHashSet();
        var accepted = await FilterSparseAsync(paths);

        await base.AddPathsAsync(accepted);
        CollectionViewSource.GetDefaultView(GameFiles)?.Refresh();

        _ = EstimateAsync([.. GameFiles.Where(f => !existing.Contains(f))]);
    }

    protected override void RemoveSelectedItems()
    {
        base.RemoveSelectedItems();
        CollectionViewSource.GetDefaultView(GameFiles)?.Refresh();
    }

    protected override void RemoveAllItems()
    {
        base.RemoveAllItems();
        CollectionViewSource.GetDefaultView(GameFiles)?.Refresh();
    }

    private async Task<List<string>> FilterSparseAsync(IEnumerable<string> paths)
    {
        var candidates = paths.Where(p => SwitchFileTypes.Extensions.Contains(Path.GetExtension(p))).ToList();

        if (!KeyExists())
            return candidates;

        var accepted = new List<string>();

        foreach (var path in candidates)
        {
            if (GameFiles.Any(f => string.Equals(f.FilePath, path, StringComparison.OrdinalIgnoreCase)))
            {
                accepted.Add(path);
                continue;
            }

            if (await Task.Run(() => Lz4RomSources.HasSparseLayer(path)))
            {
                LogRequested?.Invoke(SparseMessage(path), LogLevel.Warning);
                continue;
            }

            accepted.Add(path);
        }

        return accepted;
    }

    private async Task EstimateAsync(IReadOnlyList<GameFile> rows)
    {
        foreach (var row in rows)
        {
            if (!GameFiles.Contains(row) || !(row.FileType.Contains('B') || row.FileType.Contains('D')))
                continue;

            await _estimateGate.WaitAsync();

            try
            {
                if (!GameFiles.Contains(row))
                    continue;

                row.Lz4.MarkAnalyzing();

                int level = AppConfig.Instance.Switch.Lz4CompressLevel;
                string path = row.FilePath;
                var estimate = await Task.Run(() => new Lz4RomEstimator().EstimateFileAsync(path, level));

                row.Lz4.ApplyEstimate(estimate);
            }
            catch
            {
                row.Lz4.ApplyEstimateFailed();
            }
            finally
            {
                _estimateGate.Release();
            }
        }
    }

    private static string SparseMessage(string path) => $"{Path.GetFileName(path)}: Sparse 레이어가 있는 NCA는 Lz4 압축할 수 없어 목록에 추가하지 않습니다.";

    private void BtnExtra1_Click(object sender, RoutedEventArgs e) => ExtraButton1Clicked?.Invoke();

    private void BtnExtra2_Click(object sender, RoutedEventArgs e) => ExtraButton2Clicked?.Invoke();

    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e) => _sorter.HandleHeaderClick(e, lvFiles);
}