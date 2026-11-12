using Common.Models;
using Common.Localization;
using NSW.Core;
using RomForge.Core.Models.Switch;
using RomForge.Core.Services;
using RomForge.Core.Services.Switch;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
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

    public async Task AddFilesAsync(IEnumerable<string> paths)
    {
        var pathList = paths.ToList();

        if (pathList.Any(p => _supportedExtensions.Contains(Path.GetExtension(p))))
            RemoveUnpackedFiles();

        await _importer.AddFilesAsync(pathList, UpdateDropHint);
    }

    public async Task LoadUnpackedAsync(string unpackedDir, CancellationToken ct = default)
    {
        if (GameFiles.Any(f => !f.IsUnpacked))
            return;

        var rows = await Task.Run(() => UnpackedGameFileLoader.Load(unpackedDir), ct);

        if (ct.IsCancellationRequested || GameFiles.Any(f => !f.IsUnpacked))
            return;

        int removed = DetachAndRemoveUnpackedFiles();

        foreach (var row in rows)
            GameFiles.Add(row);

        GameFileListOrganizer.Reorganize(GameFiles);

        if (rows.Count > 0 || removed > 0)
            UpdateDropHint();

        if (rows.Count > 0)
            AppendLog(Loc.S.LogUnpackFolderLoaded(rows.Count));
    }

    private void RemoveUnpackedFiles()
    {
        if (DetachAndRemoveUnpackedFiles() > 0)
            AppendLog(Loc.S.LogUnpackFolderItemRemoved);
    }

    private int DetachAndRemoveUnpackedFiles()
    {
        var unpackedRows = GameFiles.Where(f => f.IsUnpacked).ToList();

        foreach (var row in unpackedRows)
        {
            _patchSync.Detach(row);
            GameFiles.Remove(row);
        }

        return unpackedRows.Count;
    }

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
            AppendLog(Loc.S.LogPatchAssigned(title), LogLevel.Ok);
            AppendLog($"  └ {Loc.S.LogPatchCoverage(coveredCount, totalCount, lineStr)}", LogLevel.Ok);

            if (!string.IsNullOrEmpty(extraWarningMsg))
                AppendLog($"  └ {extraWarningMsg}", LogLevel.Warning);
            else
                AppendLog($"  └ {Loc.S.LogSomeProgramsUnpatched}", LogLevel.Warning);
        }
        else
        {
            string detailMsg = Loc.S.LogPatchCoverage(coveredCount, totalCount, lineStr);

            AppendLog($"{Loc.S.LogPatchAssigned(title)}\n  └ {detailMsg}", LogLevel.Ok);
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
                    ? Loc.S.MsgPatchAssignFailedNoMatch(file.TitleName, string.Join(", ", source.FoundIds), string.Join(", ", ids))
                    : Loc.S.MsgPatchAssignFailedNotFound(file.TitleName);

                AppendLog(reason, LogLevel.Error);
                _ = SoundPlayer.PlayErrorAsync();
                MessageBox.Show(reason, Loc.S.MsgTitlePatchAssignFailed, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            path = source.GenericPath;
            bool warning = ids.Count > 1;

            if (warning)
                AppendLog(Loc.S.LogPatchAssignWarnGeneric(file.TitleName), LogLevel.Warning);
            else
                AppendLog(Loc.S.LogPatchAssignInfoGeneric(file.TitleName), LogLevel.Ok);
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
        string text = Loc.S.MsgBulkAssignSummary(report.Total, report.Matched);

        if (report.Matched == 0)
        {
            if (report.Details.Length > 0)
                text += Loc.S.MsgBulkAssignDetails(report.Details.Replace(Environment.NewLine, " | "));

            AppendLog(Loc.S.LogBulkAssignFailed(text), LogLevel.Error);
            _ = SoundPlayer.PlayErrorAsync();
            MessageBox.Show(text, Loc.S.MsgTitleBulkAssignResult, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else
        {
            AppendLog(Loc.S.LogBulkAssignDone(text), LogLevel.Ok);

            if (report.Details.Length > 0)
            {
                foreach (var line in report.Details.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Split(':');
                    if (parts.Length < 2) continue;

                    string gameTitle = parts[0].Trim();
                    string detailContent = parts[1].Trim();

                    AppendLog($"  └ {gameTitle}", LogLevel.Ok);

                    bool hasWarning = detailContent.Contains('✘') || detailContent.Contains("missing");

                    if (hasWarning)
                    {
                        AppendLog($"    └ {detailContent}", LogLevel.Ok);
                        AppendLog($"    └ {Loc.S.LogSomeProgramsUnpatched}", LogLevel.Warning);
                    }
                    else
                        AppendLog($"    └ {detailContent}", LogLevel.Ok);
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