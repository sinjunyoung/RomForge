using LibHac.Ncm;
using NSW.Core;
using NSW.Core.Models;
using NSW.WPF.Services;
using NSW.WPF.ViewModels;
using System.Collections.ObjectModel;
using System.IO;
using Res = NSW.Core.Properties.Resources;

namespace RomForge.Controls.Switch;

public sealed class SwitchGameFileImporter(ObservableCollection<GameFile> gameFiles, GameFilePatchSyncManager patchSync, HashSet<string> supportedExtensions)
{
    public async Task AddFilesAsync(IEnumerable<string> paths, Action onFileAdded)
    {
        var keySet = KeySetProvider.Instance.KeySet;
        var existing = gameFiles.Select(f => f.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var newPaths = await Task.Run(() =>
            paths.Where(p => supportedExtensions.Contains(Path.GetExtension(p)))
                 .Where(p => existing.Add(p))
                 .ToList());

        foreach (var path in newPaths)
        {
            string titleName = Path.GetFileNameWithoutExtension(path);
            System.Windows.Media.Imaging.BitmapImage? icon = null;

            if (keySet != null)
            {
                var info = MetadataReader.GetGameFileInfo(keySet, path);
                if (info != null)
                {
                    if (!string.IsNullOrEmpty(info.TitleName))
                        titleName = info.TitleName;
                    if (info.IconData != null)
                        icon = info.IconData.ToBitmapImage();
                }

                List<MetadataResult> allMeta;

                try { allMeta = MetadataReader.GetMetadataFromContainer(keySet, path); }
                catch { allMeta = []; }

                if (allMeta.Count > 0)
                {
                    var appMetas = allMeta.Where(m => m.Type == ContentMetaType.Application).ToList();

                    foreach (var app in appMetas)
                    {
                        var baseVm = new GameFile(path)
                        {
                            FileType = "B",
                            TitleID = app.TitleId,
                            Version = app.GetEffectiveDisplayVersion(),
                            TitleName = titleName,
                            Icon = icon
                        };

                        AssignOrReplace(baseVm);
                    }

                    var patchMetas = allMeta.Where(m => m.Type == ContentMetaType.Patch).ToList();

                    foreach (var patch in patchMetas)
                    {
                        var updateVm = new GameFile(path)
                        {
                            FileType = "U",
                            TitleID = patch.TitleId,
                            Version = patch.GetEffectiveDisplayVersion(),
                            TitleName = titleName,
                            Icon = icon
                        };

                        AssignOrReplace(updateVm);
                    }

                    var dlcResults = allMeta
                        .Where(m => m.Type is ContentMetaType.AddOnContent or ContentMetaType.Delta)
                        .GroupBy(m => m.TitleId, StringComparer.OrdinalIgnoreCase)
                        .Select(g => g.First())
                        .ToList();

                    foreach (var dlc in dlcResults)
                    {
                        var dlcVm = new GameFile(path)
                        {
                            FileType = "D",
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

            var vm = new GameFile(path)
            {
                FileType = keySet == null ? Res.Status_NoKey : Res.Status_Analyzing,
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

        SwitchGameFileListOrganizer.Reorganize(gameFiles);
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

            if (gameFiles.Any(f => f.FilePath.Equals(vm.FilePath, StringComparison.OrdinalIgnoreCase) && f.FileType.Contains('B')))
                return;
        }

        if (vm.FileType.Contains('U'))
        {
            var existingUpdate = gameFiles.FirstOrDefault(f => f.FileType.Contains('U') && !string.Equals(f.FilePath, vm.FilePath, StringComparison.OrdinalIgnoreCase));

            if (existingUpdate != null)
            {
                if (vm.PatchPath == null)
                {
                    vm.PatchPath = existingUpdate.PatchPath;
                    vm.PatchPassword = existingUpdate.PatchPassword;
                }

                patchSync.Detach(existingUpdate);
                gameFiles.Remove(existingUpdate);
            }

            if (gameFiles.Any(f => f.FilePath.Equals(vm.FilePath, StringComparison.OrdinalIgnoreCase) && f.FileType.Contains('U')))
                return;
        }

        if (vm.FileType.Contains('D'))
        {
            if (gameFiles.Any(f => f.FilePath.Equals(vm.FilePath, StringComparison.OrdinalIgnoreCase) && f.TitleID == vm.TitleID))
                return;
        }

        gameFiles.Add(vm);
        patchSync.Attach(vm);
        patchSync.SyncPatchToPartner(vm);
    }
}