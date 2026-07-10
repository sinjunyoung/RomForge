// WudToWuaConverter.cs
//
// Top-level orchestrator tying the whole pipeline together:
//   1. Open the .wud/.wux via WudReader (transparent decompression).
//   2. Parse the disc's partition table + FSTs via WuDiscReader, pulling
//      title.tmd out of the SI/GI partition to learn the title ID + version
//      (needed for the "titleId_vVERSION" subfolder Cemu expects inside a .wua).
//   3. Extract the GM (game) partition's file tree — this is already laid out
//      as code/ + content/ + meta/, matching the "raw" install format.
//   4. Overlay the Korean patch folder on top (whole-file replacement: any file
//      present in the patch folder at the same relative path wins).
//   5. Feed the merged tree into WuaWriter to produce the final .wua.
//
// This class only wires the pieces above together; see WudReader.cs, WuDiscReader.cs,
// TitleTicket.cs, TitleMetadata.cs and WuaWriter.cs for the actual format logic.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WiiU.Core.Services;

public sealed class WudToWuaConverter
{
    /// <param name="wudOrWuxPath">Path to the source .wud or .wux disc image.</param>
    /// <param name="discKeyPath">16-byte raw binary disc key file, specific to this disc.</param>
    /// <param name="keyProviderPath">External key file containing at least "commonKey" (see WiiUKeyProvider).</param>
    /// <param name="koreanPatchFolder">Folder whose contents overwrite matching files in the
    /// extracted code/content/meta tree, whole-file. May be null to skip patching.</param>
    /// <param name="outputWuaPath">Destination .wua path.</param>
    /// <param name="workingDirectory">Scratch folder for the intermediate decrypted dump.
    /// Needs roughly as much free space as the installed title size.</param>
    public static void Convert(
        string wudOrWuxPath,
        string discKeyPath,
        string keyProviderPath,
        string? koreanPatchFolder,
        string outputWuaPath,
        string workingDirectory)
    {
        var keys = WiiUKeyProvider.LoadFromFile(keyProviderPath);
        byte[] discKey = File.ReadAllBytes(discKeyPath);
        if (discKey.Length != 16)
            throw new InvalidDataException($"Disc key file must be exactly 16 bytes, got {discKey.Length}.");

        using var wud = WudReader.Open(wudOrWuxPath);
        var disc = WuDiscReader.Open(wud, discKey, keys);

        var gmPartition = disc.Partitions.FirstOrDefault(p => p.TypeCode == "GM" && p.Entries.Count > 0)
            ?? throw new InvalidDataException("No decryptable GM (game) partition found on this disc.");

        // title.tmd lives in the SI (or occasionally GI) partition, not inside GM itself.
        var tmdSourcePartition = disc.Partitions.FirstOrDefault(p =>
            (p.TypeCode is "SI" or "GI") && p.Entries.Any(e => !e.IsDirectory &&
                string.Equals(e.EntryName, "TITLE.TMD", StringComparison.OrdinalIgnoreCase)));

        string titleIdHex;
        ushort titleVersion;
        if (tmdSourcePartition is not null)
        {
            var tmdEntryIndex = tmdSourcePartition.Entries.FindIndex(e => !e.IsDirectory &&
                string.Equals(e.EntryName, "TITLE.TMD", StringComparison.OrdinalIgnoreCase));
            var tmdFile = new WuFileEntry { Partition = tmdSourcePartition, EntryIndex = tmdEntryIndex, FileName = "TITLE.TMD" };
            using var tmdStream = new MemoryStream();
            disc.ExtractFileTo(tmdFile, tmdStream);
            tmdStream.Position = 0;
            var tmd = TitleMetadata.Parse(tmdStream);
            titleIdHex = tmd.TitleId.ToString("x16");
            titleVersion = tmd.TitleVersion;
        }
        else
        {
            // Fallback: derive from the GM partition's own name ("GM" + 16 hex chars),
            // version left at 0 since it's only cosmetic for the subfolder name.
            titleIdHex = gmPartition.Name.Length >= 18 ? gmPartition.Name[2..18].ToLowerInvariant() : "0000000000000000";
            titleVersion = 0;
        }

        string dumpRoot = Path.Combine(workingDirectory, "dump");
        Directory.CreateDirectory(dumpRoot);
        DumpPartitionTo(disc, gmPartition, dumpRoot);

        if (koreanPatchFolder is not null)
            OverlayPatchFolder(koreanPatchFolder, dumpRoot);

        PackToWua(dumpRoot, titleIdHex, titleVersion, outputWuaPath);
    }

    private static void DumpPartitionTo(WuDiscReader disc, WuPartition partition, string destinationRoot)
    {
        foreach (var (path, file) in disc.EnumerateFiles(partition))
        {
            string destPath = Path.Combine(destinationRoot, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            using var outStream = File.Create(destPath);
            disc.ExtractFileTo(file, outStream);
        }
    }

    private static void OverlayPatchFolder(string patchFolder, string destinationRoot)
    {
        foreach (string patchFile in Directory.EnumerateFiles(patchFolder, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(patchFolder, patchFile);
            string destPath = Path.Combine(destinationRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            File.Copy(patchFile, destPath, overwrite: true);
        }
    }

    private static void PackToWua(string sourceRoot, string titleIdHex, ushort titleVersion, string outputWuaPath)
    {
        string titleFolderName = $"{titleIdHex}_v{titleVersion}";

        using var outStream = File.Create(outputWuaPath);
        using var writer = new WuaWriter(outStream);
        writer.MakeDir(titleFolderName, recursive: true);

        foreach (string dir in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceRoot, dir).Replace(Path.DirectorySeparatorChar, '/');
            writer.MakeDir($"{titleFolderName}/{relative}", recursive: true);
        }

        foreach (string file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceRoot, file).Replace(Path.DirectorySeparatorChar, '/');
            writer.StartNewFile($"{titleFolderName}/{relative}");

            using var inStream = File.OpenRead(file);
            Span<byte> buffer = new byte[1 * 1024 * 1024];
            int read;
            while ((read = inStream.Read(buffer)) > 0)
                writer.AppendData(buffer[..read]);
        }

        writer.Finalize();
    }
}