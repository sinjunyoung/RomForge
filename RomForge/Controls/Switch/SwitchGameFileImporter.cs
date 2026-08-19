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
            var vm = new GameFile(path) { FileType = keySet == null ? Res.Status_NoKey : Res.Status_Analyzing };

            if (keySet != null)
            {
                var info = MetadataReader.GetGameFileInfo(keySet, path);

                if (info != null)
                {
                    vm.TitleName = info.TitleName;
                    vm.TitleID = info.TitleId;
                    vm.Version = info.DisplayVersion;
                    vm.FileType = info.Type;

                    if (info.IconData != null) 
                        vm.Icon = info.IconData.ToBitmapImage();
                }

                List<MetadataResult> allMeta;

                try { allMeta = MetadataReader.GetMetadataFromContainer(keySet, path); }
                catch { allMeta = []; }

                var dlcResults = allMeta
                    .Where(m => m.Type is ContentMetaType.AddOnContent or ContentMetaType.Delta)
                    .GroupBy(m => m.TitleId, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                if (dlcResults.Count > 0)
                {
                    bool hasBaseOrUpdate = vm.FileType.Contains('B') || vm.FileType.Contains('U');

                    vm.FileType = string.Concat(vm.FileType.Where(c => c != 'D'));

                    foreach (var dlc in dlcResults)
                    {
                        var dlcVm = new GameFile(path)
                        {
                            FileType = "D",
                            TitleID = dlc.TitleId,
                            Version = dlc.GetEffectiveDisplayVersion(),
                            TitleName = string.IsNullOrEmpty(vm.TitleName) ? dlc.TitleId : $"{vm.TitleName} (DLC {dlc.TitleId[^4..]})",
                            Icon = vm.Icon,
                        };

                        AssignOrReplace(dlcVm);
                    }

                    if (!hasBaseOrUpdate)
                    {
                        onFileAdded();
                        continue;
                    }
                }
            }

            if (string.IsNullOrEmpty(vm.TitleName))
                vm.TitleName = Path.GetFileNameWithoutExtension(path);

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
            var existingBase = gameFiles.FirstOrDefault(f => f.FileType.Contains('B'));

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
            var existingUpdate = gameFiles.FirstOrDefault(f => f.FileType.Contains('U'));

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
        }

        gameFiles.Add(vm);
        patchSync.Attach(vm);
        patchSync.SyncPatchToPartner(vm);
    }
}