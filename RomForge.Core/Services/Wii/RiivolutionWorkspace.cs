using Patch.Core.Services;
using RomForge.Core.Models.Wii;
using System.IO;

namespace RomForge.Core.Services.Wii;

public sealed class RiivolutionWorkspace : IDisposable
{
    private const int CopyBufferSize = 0x100000;
    private const int MaxRootSearchDepth = 3;

    private readonly string? _tempRoot;

    public string SourcePath { get; }

    public string RootPath { get; }

    public RiivolutionSourceKind Kind { get; }

    private RiivolutionWorkspace(string sourcePath, string rootPath, RiivolutionSourceKind kind, string? tempRoot)
    {
        SourcePath = sourcePath;
        RootPath = rootPath;
        Kind = kind;
        _tempRoot = tempRoot;
    }

    public static bool IsSupported(string path)
    {
        if (Directory.Exists(path))
            return true;

        if (!File.Exists(path))
            return false;

        string extension = Path.GetExtension(path);

        return IsXml(extension) || IsZip(extension) || Is7z(extension);
    }

    public static bool IsArchive(string path)
    {
        if (!File.Exists(path))
            return false;

        string extension = Path.GetExtension(path);

        return IsZip(extension) || Is7z(extension);
    }

    public static RiivolutionWorkspace OpenLocal(string path)
    {
        if (Directory.Exists(path))
            return new RiivolutionWorkspace(path, ResolveRoot(path), RiivolutionSourceKind.Folder, null);

        if (File.Exists(path) && IsXml(Path.GetExtension(path)))
            return new RiivolutionWorkspace(path, Path.GetFullPath(path), RiivolutionSourceKind.Xml, null);

        throw new InvalidDataException("지원하지 않는 패치 형식입니다 (폴더, zip, 7z, xml만 가능).");
    }

    public static async Task<RiivolutionWorkspace> ExtractAsync(string archivePath, IArchivePatchSource archive, Action<double>? progress, CancellationToken ct)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "RomForge_Riivolution_" + Guid.NewGuid().ToString("N"));

        try
        {
            await Task.Run(() => ExtractAll(archive, tempRoot, progress, ct), ct);

            var kind = Is7z(Path.GetExtension(archivePath)) ? RiivolutionSourceKind.SevenZip : RiivolutionSourceKind.Zip;

            return new RiivolutionWorkspace(archivePath, ResolveRoot(tempRoot), kind, tempRoot);
        }
        catch
        {
            DeleteQuietly(tempRoot);

            throw;
        }
        finally
        {
            archive.Dispose();
        }
    }

    public void Dispose()
    {
        if (_tempRoot != null)
            DeleteQuietly(_tempRoot);
    }

    private static void ExtractAll(IArchivePatchSource archive, string tempRoot, Action<double>? progress, CancellationToken ct)
    {
        Directory.CreateDirectory(tempRoot);

        string boundary = Path.GetFullPath(tempRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var entries = archive.EntryPaths.Select(archive.FindEntry).OfType<IArchivePatchEntry>().ToList();
        long total = Math.Max(1, entries.Sum(e => e.Length));
        long done = 0;
        byte[] buffer = new byte[CopyBufferSize];

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();

            string destination = Path.GetFullPath(Path.Combine(tempRoot, entry.FullPath.Replace('/', Path.DirectorySeparatorChar)));

            if (!destination.StartsWith(boundary, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"압축 파일에 안전하지 않은 경로가 있습니다: {entry.FullPath}");

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            using var input = entry.Open();
            using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize);

            int read;

            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();

                output.Write(buffer, 0, read);

                done += read;

                progress?.Invoke((double)done / total);
            }
        }

        progress?.Invoke(1);
    }

    private static string ResolveRoot(string directory)
    {
        var queue = new Queue<(string Path, int Depth)>();

        queue.Enqueue((directory, 0));

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();

            if (IsPatchRoot(current))
                return current;

            if (depth >= MaxRootSearchDepth)
                continue;

            string[] subDirectories;

            try { subDirectories = Directory.GetDirectories(current); }
            catch { continue; }

            foreach (string sub in subDirectories)
                queue.Enqueue((sub, depth + 1));
        }

        return directory;
    }

    private static bool IsPatchRoot(string directory)
    {
        if (Directory.Exists(Path.Combine(directory, "riivolution")))
            return true;

        return Directory.EnumerateFiles(directory, "*.xml", SearchOption.TopDirectoryOnly).Any();
    }

    private static void DeleteQuietly(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
        catch { }
    }

    private static bool IsXml(string extension) => string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase);

    private static bool IsZip(string extension) => string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase);

    private static bool Is7z(string extension) => string.Equals(extension, ".7z", StringComparison.OrdinalIgnoreCase);
}