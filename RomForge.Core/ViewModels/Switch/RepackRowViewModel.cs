using Common;
using NSW.Core;
using RomForge.Core.Models.Switch;
using RomForge.Core.Services;
using RomForge.Core.Services.Switch;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;

namespace RomForge.Core.ViewModels.Switch;

public class RepackRowViewModel : ToolTabViewModel
{
    private readonly GameFilePatchSyncManager _patchSync;
    private readonly GameFileImporter _importer;
    private readonly HashSet<string> _supportedExtensions;

    public ObservableCollection<GameFile> GameFiles { get; } = [];

    public Action<string, LogLevel>? LogAction { get; set; }

    public event Action? FileListChanged;

    public RepackRowViewModel(HashSet<string> supportedExtensions)
    {
        _supportedExtensions = supportedExtensions;
        _patchSync = new GameFilePatchSyncManager(GameFiles);
        _importer = new GameFileImporter(GameFiles, _patchSync, _supportedExtensions);
    }

    public void UpdateDropHint() => FileListChanged?.Invoke();

    public async Task AddFilesAsync(IEnumerable<string> paths) => await _importer.AddFilesAsync(paths, UpdateDropHint);

    public void RecalcKeyMissingFiles(Action onCompleted)
    {
        var targets = GameFiles.Where(f => f.IsKeyMissing).ToList();

        if (targets.Count == 0)
        {
            onCompleted();
            return;
        }

        var keySet = KeySetProvider.Instance.KeySet;

        if (keySet == null)
        {
            onCompleted();
            return;
        }

        int remaining = targets.Count;

        foreach (var vm in targets)
        {
            string capturedPath = vm.FilePath;

            _ = Task.Run(() =>
            {
                string result = MetadataReader.DetectFileType(keySet, capturedPath);

                if (Interlocked.Decrement(ref remaining) == 0)
                    Application.Current.Dispatcher.Invoke(() => { vm.FileType = result; onCompleted(); });
                else
                    Application.Current.Dispatcher.Invoke(() => { vm.FileType = result; });
            });
        }
    }

    public IReadOnlyList<string> GetExpectedIds(GameFile file)
    {
        if (file.FileType.Contains('D'))
            return [(file.TitleID ?? string.Empty).ToUpperInvariant()];

        var baseRow = GameFiles.FirstOrDefault(f => f.FileType.Contains('B'));
        var updateRow = GameFiles.FirstOrDefault(f => f.FileType.Contains('U'));
        string? baseId = baseRow?.TitleID;

        if (string.IsNullOrEmpty(baseId) && ulong.TryParse(updateRow?.TitleID, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong updateValue))
            baseId = (updateValue & ~0x800UL).ToString("X16");

        if (!ulong.TryParse(baseId, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong baseValue))
            return [(file.TitleID ?? string.Empty).ToUpperInvariant()];

        IEnumerable<byte> baseOffsets = baseRow?.ProgramIdOffsets ?? Enumerable.Empty<byte>();
        IEnumerable<byte> updateOffsets = updateRow?.ProgramIdOffsets ?? Enumerable.Empty<byte>();
        var offsets = baseOffsets.Concat(updateOffsets).Distinct().OrderBy(o => o).ToList();

        if (offsets.Count == 0)
            offsets.Add(0);

        return [.. offsets.Select(o => (baseValue + o).ToString("X16"))];
    }

    private void LogPatchResult(string? title, int coveredCount, int totalCount, IEnumerable<string> lines, bool hasWarning, string? extraWarningMsg = null)
    {
        string lineStr = string.Join(", ", lines);

        if (hasWarning)
        {
            AppendLog($"[패치 지정 완료] {title}", LogLevel.Ok);
            AppendLog($"  ㄴ 프로그램 {coveredCount}/{totalCount} 패치 확인 ({lineStr})", LogLevel.Ok);

            if (!string.IsNullOrEmpty(extraWarningMsg))
                AppendLog($"  ㄴ {extraWarningMsg}", LogLevel.Warning);
            else
                AppendLog($"  ㄴ 일부 프로그램은 패치가 없어 원본 그대로 빌드됩니다.", LogLevel.Warning);
        }
        else
        {
            string detailMsg = $"프로그램 {coveredCount}/{totalCount} 패치 확인 ({lineStr})";
            AppendLog($"[패치 지정 완료] {title}\n  ㄴ {detailMsg}", LogLevel.Ok);
        }
    }

    public bool TryAssignPatch(GameFile file, PatchSource source, string? password)
    {
        var ids = GetExpectedIds(file);
        var match = source.Match(ids);
        string? path = match.Path;

        if (path == null)
        {
            if (source.HasIdDirs || !source.HasGeneric)
            {
                string reason = source.HasIdDirs
                    ? $"[패치 지정 실패] {file.TitleName}: 이 게임과 일치하는 타이틀 ID 폴더가 없습니다.\n패치 ID: {string.Join(", ", source.FoundIds)} / 게임 ID: {string.Join(", ", ids)}"
                    : $"[패치 지정 실패] {file.TitleName}: 패치에서 타이틀 ID 폴더나 exefs/romfs/ips/xdelta를 찾지 못했습니다.";

                AppendLog(reason, LogLevel.Error);
                _ = SoundPlayer.PlayErrorAsync();
                MessageBox.Show(reason, "패치 지정 실패", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            path = source.GenericPath;
            bool warning = ids.Count > 1;

            if (warning)
                AppendLog($"[패치 지정 경고] {file.TitleName}: 타이틀 ID 폴더가 없는 일반 패치(exefs/romfs)로 지정했습니다. 프로그램이 여러 개인 게임이라 모든 프로그램에 같은 파일이 적용됩니다.", LogLevel.Warning);
            else
                AppendLog($"[패치 지정 안내] {file.TitleName}: 타이틀 ID 폴더가 없는 일반 패치(exefs/romfs)로 지정했습니다.", LogLevel.Ok);
        }
        else
        {
            bool warning = match.Missing.Count > 0;
            var lines = ids.Select(id => $"{(match.Covered.Contains(id, StringComparer.OrdinalIgnoreCase) ? "✔" : "✘")} {id}");

            LogPatchResult(file.TitleName, match.Covered.Count, ids.Count, lines, warning);
        }

        file.PatchPath = path;
        file.PatchPassword = password;
        return true;
    }

    public void ProcessBulkPatchResult(BulkPatchReport report)
    {
        string text = $"총 {report.Total}개 중 {report.Matched}개에 패치 지정됨.";

        if (report.Matched == 0)
        {
            if (report.Details.Length > 0)
                text += $"\n상세: {report.Details.Replace(Environment.NewLine, " | ")}";

            AppendLog($"[일괄 지정 실패] {text}", LogLevel.Error);
            _ = SoundPlayer.PlayErrorAsync();
            MessageBox.Show(text, "일괄 지정 결과", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else
        {
            AppendLog($"[일괄 지정 완료] {text}", LogLevel.Ok);

            if (report.Details.Length > 0)
            {
                foreach (var line in report.Details.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Split(':');
                    if (parts.Length < 2) continue;

                    string gameTitle = parts[0].Trim();
                    string detailContent = parts[1].Trim();

                    AppendLog($"  ㄴ {gameTitle}", LogLevel.Ok);

                    bool hasWarning = detailContent.Contains("패치 없음") || detailContent.Contains('✘') || detailContent.Contains("missing");

                    if (hasWarning)
                    {
                        AppendLog($"    ㄴ {detailContent}", LogLevel.Ok);
                        AppendLog($"    ㄴ 일부 프로그램은 패치가 없어 원본 그대로 빌드됩니다.", LogLevel.Warning);
                    }
                    else
                    {
                        AppendLog($"    ㄴ {detailContent}", LogLevel.Ok);
                    }
                }
            }
        }
    }

    public void RemoveFile(GameFile item)
    {
        _patchSync.Detach(item);
        GameFiles.Remove(item);
        GameFileListOrganizer.Reorganize(GameFiles);
        UpdateDropHint();
    }

    public void RemoveAllFiles()
    {
        foreach (var item in GameFiles)
            _patchSync.Detach(item);

        GameFiles.Clear();
        UpdateDropHint();
    }

    public void ClearPatch(GameFile file) => _patchSync.ClearPatch(file);

    public override void AppendLog(string msg, LogLevel level = LogLevel.Info) => LogAction?.Invoke(msg, level);
}