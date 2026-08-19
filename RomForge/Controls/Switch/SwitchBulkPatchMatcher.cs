using NSW.WPF.ViewModels;
using Patch.Core.Services;
using System.IO;

namespace RomForge.Controls.Switch;

public static class SwitchBulkPatchMatcher
{
    public static int MatchFromFolder(IEnumerable<GameFile> targets, string rootFolder)
    {
        int matched = 0;

        foreach (var file in targets)
        {
            string folderCandidate = Path.Combine(rootFolder, file.TitleID!);

            if (Directory.Exists(folderCandidate))
            {
                file.PatchPath = folderCandidate;
                matched++;
                continue;
            }

            string? recursiveMatch = PatchFolderResolver.FindSubDir(rootFolder, file.TitleID!);

            if (recursiveMatch != null)
            {
                file.PatchPath = recursiveMatch;
                matched++;
                continue;
            }

            string[] exts = [".zip", ".7z"];
            string? archiveCandidate = exts
                .Select(ext => Path.Combine(rootFolder, file.TitleID! + ext))
                .FirstOrDefault(File.Exists);

            if (archiveCandidate != null)
            {
                file.PatchPath = archiveCandidate;
                matched++;
            }
        }

        return matched;
    }

    public static int MatchFromArchive(IEnumerable<GameFile> targets, IArchivePatchSource archive, string archivePath, string? password)
    {
        int matched = 0;

        foreach (var file in targets)
        {
            string? prefix = ArchivePatchFolderResolver.FindSubDir(archive.EntryPaths, file.TitleID!);

            if (prefix == null)
                continue;

            file.PatchPath = ArchivePatchSourceFactory.CombineScope(archivePath, prefix);
            file.PatchPassword = password;
            matched++;
        }

        return matched;
    }
}
