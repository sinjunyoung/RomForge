using Common;
using Patch.Core.Services;
using WiiU.Core.Models;

namespace WiiU.Core.Services;

public sealed class WiiURepackService
{
    private const int BufferSize = 1024 * 1024;
    public static readonly string[] PatchAnchors = ["content", "meta", "code"];

    public static void Repack(ITitleSource source, string outputWuaPath, string? patchFolder = null, string? titleIdHexOverride = null, int? titleVersionOverride = null, Action<long, long, string>? onProgress = null, Action<string, LogLevel>? log = null, CancellationToken ct = default)
        => RepackMultiple([new RepackEntry(source, patchFolder, titleIdHexOverride, titleVersionOverride)], outputWuaPath, onProgress, log, ct);

    public static void RepackMultiple(IReadOnlyList<RepackEntry> entries, string outputWuaPath, Action<long, long, string>? onProgress = null, Action<string, LogLevel>? log = null, CancellationToken ct = default)
    {
        if (entries.Count == 0)
            throw new ArgumentException("At least one entry is required.", nameof(entries));

        var resolved = new List<(string TitleFolder, ITitleSource Source, PatchSession? Patch, List<string> Paths)>();
        var seenFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sessions = new List<PatchSession>();

        try
        {
            foreach (var entry in entries)
            {
                string titleIdHex = entry.TitleIdHexOverride ?? entry.Source.TitleIdHex;
                int titleVersion = entry.TitleVersionOverride ?? entry.Source.TitleVersion;
                string titleFolder = $"{titleIdHex}_v{titleVersion}";

                if (!seenFolders.Add(titleFolder))
                    throw new InvalidOperationException($"Two entries both resolved to the folder name \"{titleFolder}\" — they'd collide in the output .wua. " + "Check that base/update/DLC don't share the same title ID + version.");

                var sourcePaths = new HashSet<string>(entry.Source.EnumerateFiles(), StringComparer.Ordinal);
                PatchSession? patch = null;

                if (entry.PatchFolder is not null)
                {
                    patch = PatchSession.Open(entry.PatchFolder, PatchAnchors, sourcePaths, log, titleFolder);

                    sessions.Add(patch);
                }

                var paths = new SortedSet<string>(sourcePaths, StringComparer.Ordinal);

                resolved.Add((titleFolder, entry.Source, patch, new List<string>(paths)));
            }

            long total = 0;

            foreach (var (_, source, patch, paths) in resolved)
            {
                foreach (string path in paths)
                    total += patch is not null && patch.Plan.TryGetOverwrite(path, out var overwriteRef) ? overwriteRef.Length : source.GetFileSize(path);
            }

            var progress = new ByteProgress(total, onProgress);
            using var outStream = File.Create(outputWuaPath);
            using var writer = new WuaWriter(outStream);
            var buffer = new byte[BufferSize];

            foreach (var (titleFolder, source, patch, paths) in resolved)
            {
                writer.MakeDir(titleFolder, recursive: true);

                var writtenDirs = new HashSet<string>(StringComparer.Ordinal);

                foreach (string path in paths)
                {
                    ct.ThrowIfCancellationRequested();
                    EnsureDirWritten(writer, titleFolder, GetDirectoryPart(path), writtenDirs);

                    string label = $"{titleFolder}/{path}";

                    writer.StartNewFile(label);
                    progress.Advance(0, label);

                    using Stream srcStream = OpenEntry(path, source, patch, log, ct);
                    int read;

                    while ((read = srcStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        writer.AppendData(buffer.AsSpan(0, read));
                        progress.Advance(read, label);
                    }
                }
            }

            writer.FinalizeArchive();
            progress.Complete(string.Empty);
        }
        finally
        {
            foreach (var session in sessions)
                session.Dispose();
        }
    }

    private static Stream OpenEntry(string path, ITitleSource source, PatchSession? patch, Action<string, LogLevel>? log, CancellationToken ct) => patch is null ? source.OpenRead(path) : patch.OpenPatched(path, () => source.OpenRead(path), source.GetFileSize(path), log, ct);

    private static string GetDirectoryPart(string path)
    {
        int idx = path.LastIndexOf('/');
        return idx < 0 ? string.Empty : path[..idx];
    }

    private static void EnsureDirWritten(WuaWriter writer, string titleFolderName, string dirPath, HashSet<string> writtenDirs)
    {
        if (dirPath.Length == 0 || !writtenDirs.Add(dirPath))
            return;

        EnsureDirWritten(writer, titleFolderName, GetDirectoryPart(dirPath), writtenDirs);
        writer.MakeDir($"{titleFolderName}/{dirPath}", recursive: true);
    }
}