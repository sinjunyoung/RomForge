using LibHac.Ncm;
using NSW.Core;
using NSW.Core.Models;
using RomForge.Core.ViewModels.Switch;
using System.Collections.ObjectModel;
using System.IO;
using Res = NSW.Properties.Resources;

namespace RomForge.Core.Services.Switch;

public sealed class GameFileImporter(ObservableCollection<GameFile> gameFiles, GameFilePatchSyncManager patchSync, HashSet<string> supportedExtensions)
{
    public async Task AddFilesAsync(IEnumerable<string> paths, Action onFileAdded)
    {
        var keySet = KeySetProvider.Instance.KeySet;
        var validPaths = await Task.Run(() =>
            paths.Where(p => supportedExtensions.Contains(Path.GetExtension(p)))
                 .ToList());

        foreach (var path in validPaths)
        {
            string titleName = Path.GetFileNameWithoutExtension(path);
            System.Windows.Media.Imaging.BitmapImage? icon = null;
            long fileLength = 0;

            try
            {
                fileLength = new FileInfo(path).Length;
            }
            catch { }

            if (keySet != null)
            {
                List<MetadataResult> allMeta;

                try
                {
                    allMeta = MetadataReader.GetMetadataFromContainer(keySet, path);
                }
                catch
                {
                    allMeta = [];
                }

                if (allMeta.Count > 0)
                {
                    var appMetas = allMeta.Where(m => m.Type == ContentMetaType.Application).ToList();
                    var patchMetas = allMeta.Where(m => m.Type == ContentMetaType.Patch).ToList();
                    var dlcResults = allMeta
                        .Where(m => m.Type is ContentMetaType.AddOnContent or ContentMetaType.Delta)
                        .GroupBy(m => m.TitleId, StringComparer.OrdinalIgnoreCase)
                        .Select(g => g.First())
                        .ToList();

                    var expectedItems = new List<(string FileType, string TitleId)>();

                    foreach (var app in appMetas)
                        expectedItems.Add(("B", app.TitleId ?? string.Empty));

                    foreach (var patch in patchMetas)
                        expectedItems.Add(("U", patch.TitleId ?? string.Empty));

                    foreach (var dlc in dlcResults) 
                        expectedItems.Add(("D", dlc.TitleId ?? string.Empty));

                    var currentItemsForFile = gameFiles.Where(f => string.Equals(f.FilePath, path, StringComparison.OrdinalIgnoreCase)).ToList();
                    bool isAlreadyFullyAdded = expectedItems.Count > 0 && expectedItems.All(exp =>
                        currentItemsForFile.Any(curr => curr.FileType.Contains(exp.FileType) && string.Equals(curr.TitleID, exp.TitleId, StringComparison.OrdinalIgnoreCase)));

                    if (isAlreadyFullyAdded)
                        continue;

                    foreach (var old in currentItemsForFile)
                    {
                        patchSync.Detach(old);
                        gameFiles.Remove(old);
                    }

                    var info = MetadataReader.GetGameFileInfo(keySet, path);

                    if (info != null)
                    {
                        if (!string.IsNullOrEmpty(info.TitleName))
                            titleName = info.TitleName;

                        if (info.IconData != null)
                            icon = info.IconData.ToBitmapImage();
                    }

                    foreach (var app in appMetas)
                    {
                        var baseVm = new GameFile(path)
                        {
                            FileType = "B",
                            CustomSizeBytes = app.Size > 0 ? app.Size : fileLength,
                            ProgramIdOffsets = app.ProgramIdOffsets ?? [],
                            SourceHasCompressedRomfs = app.HasCompressedRomfs,
                            TitleID = app.TitleId,
                            Version = app.GetEffectiveDisplayVersion(),
                            TitleName = titleName,
                            Icon = icon
                        };

                        AssignOrReplace(baseVm);
                    }

                    foreach (var patch in patchMetas)
                    {
                        var updateVm = new GameFile(path)
                        {
                            FileType = "U",
                            CustomSizeBytes = patch.Size > 0 ? patch.Size : fileLength,
                            ProgramIdOffsets = patch.ProgramIdOffsets ?? [],
                            SourceHasCompressedRomfs = patch.HasCompressedRomfs,
                            TitleID = patch.TitleId,
                            Version = patch.GetEffectiveDisplayVersion(),
                            TitleName = titleName,
                            Icon = icon
                        };

                        AssignOrReplace(updateVm);
                    }

                    foreach (var dlc in dlcResults)
                    {
                        var dlcVm = new GameFile(path)
                        {
                            FileType = "D",
                            CustomSizeBytes = dlc.Size > 0 ? dlc.Size : fileLength,
                            SourceHasCompressedRomfs = dlc.HasCompressedRomfs,
                            TitleID = dlc.TitleId,
                            Version = dlc.GetEffectiveDisplayVersion(),
                            TitleName = string.IsNullOrEmpty(titleName) ? dlc.TitleId : $"{titleName} (DLC {dlc.TitleId[^4..]})",
                            Icon = icon
                        };

                        AssignOrReplace(dlcVm);
                    }

                    if (appMetas.Count > 0 || patchMetas.Count > 0 || dlcResults.Count > 0)
                    {
                        onFileAdded();
                        continue;
                    }
                }
            }

            var existingSingle = gameFiles.FirstOrDefault(f => string.Equals(f.FilePath, path, StringComparison.OrdinalIgnoreCase));

            if (existingSingle != null)
                continue;

            var vm = new GameFile(path)
            {
                FileType = keySet == null ? Res.Status_NoKey : Res.Status_Analyzing,
                CustomSizeBytes = fileLength,
                TitleName = titleName,
                Icon = icon
            };

            if (keySet != null)
            {
                var info = MetadataReader.GetGameFileInfo(keySet, path);

                if (info != null)
                {
                    vm.TitleID = info.TitleId;
                    vm.Version = info.DisplayVersion;
                    vm.FileType = info.Type;
                }
            }

            AssignOrReplace(vm);
            onFileAdded();
        }

        GameFileListOrganizer.Reorganize(gameFiles);
        onFileAdded();
    }

    private void AssignOrReplace(GameFile vm)
    {
        if (vm.FileType.Contains('B'))
        {
            var existingBase = gameFiles.FirstOrDefault(f => f.FileType.Contains('B') && !string.Equals(f.FilePath, vm.FilePath, StringComparison.OrdinalIgnoreCase));

            if (existingBase != null)
            {
                if (vm.PatchPath == null)
                {
                    vm.PatchPath = existingBase.PatchPath;
                    vm.PatchPassword = existingBase.PatchPassword;
                }

                patchSync.Detach(existingBase);
                gameFiles.Remove(existingBase);
            }
        }

        if (vm.FileType.Contains('U'))
        {
            var existingUpdate = gameFiles.FirstOrDefault(f => f.FileType.Contains('U') && !string.Equals(f.FilePath, vm.FilePath, StringComparison.OrdinalIgnoreCase));

            if (existingUpdate != null)
            {
                if (vm.PatchPath == null)
                {
                    vm.PatchPath = existingUpdate.PatchPath;
                    vm.PatchPassword = existingUpdate.PatchPath;
                }

                patchSync.Detach(existingUpdate);
                gameFiles.Remove(existingUpdate);
            }
        }

        if (vm.FileType.Contains('D'))
        {
            var existingDlc = gameFiles.FirstOrDefault(f => f.FileType.Contains('D') && f.TitleID != null && f.TitleID.Equals(vm.TitleID, StringComparison.OrdinalIgnoreCase) && !string.Equals(f.FilePath, vm.FilePath, StringComparison.OrdinalIgnoreCase));

            if (existingDlc != null)
            {
                if (vm.PatchPath == null)
                {
                    vm.PatchPath = existingDlc.PatchPath;
                    vm.PatchPassword = existingDlc.PatchPassword;
                }

                patchSync.Detach(existingDlc);
                gameFiles.Remove(existingDlc);
            }
        }

        if (gameFiles.Any(f => f.FilePath.Equals(vm.FilePath, StringComparison.OrdinalIgnoreCase) && f.FileType == vm.FileType && f.TitleID == vm.TitleID))
            return;

        gameFiles.Add(vm);
        patchSync.Attach(vm);
        patchSync.SyncPatchToPartner(vm);
    }
}