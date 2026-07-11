using System.IO;
using WiiU.Core.Services;

namespace RomForge.Core.Services.WiiU;

public sealed class RepackService
{
    private const int BufferSize = 1024 * 1024;

    public static void Repack(ITitleSource source, string outputWuaPath, string? patchFolder = null, string? titleIdHexOverride = null, int? titleVersionOverride = null)
    {
        string titleIdHex = titleIdHexOverride ?? source.TitleIdHex;
        int titleVersion = titleVersionOverride ?? source.TitleVersion;
        string titleFolderName = $"{titleIdHex}_v{titleVersion}";

        var patchFiles = new Dictionary<string, string>(StringComparer.Ordinal);

        if (patchFolder is not null)
        {
            foreach (string file in Directory.EnumerateFiles(patchFolder, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(patchFolder, file).Replace(Path.DirectorySeparatorChar, '/');

                patchFiles[relative] = file;
            }
        }

        var allPaths = new SortedSet<string>(source.EnumerateFiles(), StringComparer.Ordinal);

        foreach (var p in patchFiles.Keys) 
            allPaths.Add(p);

        using var outStream = File.Create(outputWuaPath);
        using var writer = new WuaWriter(outStream);

        writer.MakeDir(titleFolderName, recursive: true);

        var writtenDirs = new HashSet<string>(StringComparer.Ordinal);
        var buffer = new byte[BufferSize];

        foreach (string path in allPaths)
        {
            EnsureDirWritten(writer, titleFolderName, GetDirectoryPart(path), writtenDirs);
            writer.StartNewFile($"{titleFolderName}/{path}");

            using Stream srcStream = patchFiles.TryGetValue(path, out var patchFilePath) ? File.OpenRead(patchFilePath) : source.OpenRead(path);

            int read;

            while ((read = srcStream.Read(buffer, 0, buffer.Length)) > 0)
                writer.AppendData(buffer.AsSpan(0, read));
        }

        writer.FinalizeArchive();
    }

    private static string GetDirectoryPart(string path)
    {
        int idx = path.LastIndexOf('/');

        return idx < 0 ? "" : path[..idx];
    }

    private static void EnsureDirWritten(WuaWriter writer, string titleFolderName, string dirPath, HashSet<string> writtenDirs)
    {
        if (dirPath.Length == 0 || !writtenDirs.Add(dirPath)) 
            return;

        EnsureDirWritten(writer, titleFolderName, GetDirectoryPart(dirPath), writtenDirs);
        writer.MakeDir($"{titleFolderName}/{dirPath}", recursive: true);
    }
}