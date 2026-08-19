using NSW.WPF.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RomForge.Controls.Switch;

public sealed class GameFilePatchSyncManager(ObservableCollection<GameFile> gameFiles)
{
    private bool _syncingPatch;

    public void Attach(GameFile vm) => vm.PropertyChanged += GameFile_PatchPropertyChanged;

    public void Detach(GameFile vm) => vm.PropertyChanged -= GameFile_PatchPropertyChanged;

    private void GameFile_PatchPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_syncingPatch)
            return;

        if (sender is not GameFile file)
            return;

        if (e.PropertyName != nameof(GameFile.PatchPath) && e.PropertyName != nameof(GameFile.PatchPassword))
            return;

        SyncPatchToPartner(file);
    }

    public GameFile? FindPatchPartner(GameFile file)
    {
        bool isBase = file.FileType.Contains('B');
        bool isUpdate = file.FileType.Contains('U');

        if (!isBase && !isUpdate)
            return null;

        return isBase ? gameFiles.FirstOrDefault(f => f.FileType.Contains('U')) : gameFiles.FirstOrDefault(f => f.FileType.Contains('B'));
    }

    public void SyncPatchToPartner(GameFile file)
    {
        var partner = FindPatchPartner(file);

        if (partner == null || ReferenceEquals(partner, file))
            return;

        if (string.IsNullOrEmpty(file.PatchPath) && !string.IsNullOrEmpty(partner.PatchPath))
        {
            ApplySyncedPatch(file, partner.PatchPath, partner.PatchPassword);
            return;
        }

        if (partner.PatchPath == file.PatchPath && partner.PatchPassword == file.PatchPassword)
            return;

        ApplySyncedPatch(partner, file.PatchPath, file.PatchPassword);
    }

    public void ApplySyncedPatch(GameFile target, string? patchPath, string? patchPassword)
    {
        _syncingPatch = true;

        try
        {
            target.PatchPath = patchPath;
            target.PatchPassword = patchPassword;
        }
        finally
        {
            _syncingPatch = false;
        }
    }

    public void ClearPatch(GameFile file)
    {
        var partner = FindPatchPartner(file);

        _syncingPatch = true;

        try
        {
            file.PatchPath = null;
            file.PatchPassword = null;

            if (partner != null)
            {
                partner.PatchPath = null;
                partner.PatchPassword = null;
            }
        }
        finally
        {
            _syncingPatch = false;
        }
    }
}