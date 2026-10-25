using Common;
using N3DS.Core.Models;
using Patch.Core;
using Patch.Core.Enums;
using Patch.Core.Services;
using System.Security.Cryptography;
using System.Text;

namespace N3DS.Core.Services;

public static class ExeFsPacker
{
    private const int BlockSize = 0x200;
    private const int MaxEntries = 8;
    private const int ExHeaderCompressFlagOffset = 0x0D;
    private const byte ExHeaderCompressFlagBit = 0x01;

    public static byte[] Pack(IReadOnlyList<ExeFsFile> files)
    {
        if (files.Count == 0)
            throw new ArgumentException("ExeFS에 파일이 없습니다.");

        if (files.Count > MaxEntries)
            throw new ArgumentException($"ExeFS 최대 파일 수({MaxEntries}) 초과");

        uint totalSize = ExeFsHeader.Size;

        foreach (var f in files)
            totalSize += AlignUp((uint)f.Data.Length, BlockSize);

        byte[] buf = new byte[totalSize];
        uint currentOffset = 0;

        for (int i = 0; i < files.Count; i++)
        {
            var file = files[i];

            if (i > 0)
                currentOffset = AlignUp(currentOffset + (uint)files[i - 1].Data.Length, BlockSize);

            int entryBase = i * 0x10;
            byte[] nameBytes = Encoding.ASCII.GetBytes(file.Name);

            Array.Copy(nameBytes, 0, buf, entryBase, Math.Min(nameBytes.Length, 8));
            BitConverter.GetBytes(currentOffset).CopyTo(buf, entryBase + 8);
            BitConverter.GetBytes((uint)file.Data.Length).CopyTo(buf, entryBase + 12);

            int hashBase = 0x100 + (MaxEntries - 1 - i) * 0x20;

            SHA256.HashData(file.Data).CopyTo(buf, hashBase);
        }

        currentOffset = 0;

        for (int i = 0; i < files.Count; i++)
        {
            if (i > 0)
                currentOffset = AlignUp(currentOffset + (uint)files[i - 1].Data.Length, BlockSize);

            int dataPos = ExeFsHeader.Size + (int)currentOffset;

            files[i].Data.CopyTo(buf, dataPos);
        }

        return buf;
    }

    public static async Task<byte[]> PackFromDirectoryAsync(string exefsDir, CancellationToken ct = default)
    {
        var fileInfos = Directory.GetFiles(exefsDir, "*.bin")
            .Select(path =>
            {
                string baseName = Path.GetFileNameWithoutExtension(path);
                string exefsName = baseName == "code" ? ".code" : baseName;

                return (path, exefsName);
            })
            .OrderBy(x => x.exefsName == ".code" ? 0 : 1)
            .ThenBy(x => x.exefsName)
            .ToList();

        var files = new List<ExeFsFile>();

        foreach (var (path, name) in fileInfos)
        {
            byte[] data = await File.ReadAllBytesAsync(path, ct);

            files.Add(new ExeFsFile
            {
                Name = name,
                Data = data,
                ExpectedHash = [],
                HashValid = false,
            });
        }

        return Pack(files);
    }

    public static async Task<(byte[] Data, int PatchedCount)> PackWithPatchAsync(IReadOnlyList<ExeFsFile> originalFiles, PatchFileIndex? exefsIndex, byte[]? exHeader, PatchFileIndex? rootIndex, Action<string, LogLevel>? log = null, CancellationToken ct = default)
    {
        if (exefsIndex == null && rootIndex == null)
            return (Pack(originalFiles), 0);

        var patchedFiles = new List<ExeFsFile>();
        int patchedCount = 0;

        foreach (var file in originalFiles)
        {
            string baseName = file.Name == ".code" ? "code" : file.Name;
            bool allowRootFallback = file.Name == ".code";

            var (binRef, ipsRef) = ResolvePatchFiles(baseName, exefsIndex, allowRootFallback ? rootIndex : null);

            if (binRef != null)
            {
                byte[] patchData = await binRef.ReadSmallFileBytesAsync(ct);

                patchedFiles.Add(new ExeFsFile
                {
                    Name = file.Name,
                    Data = patchData,
                    ExpectedHash = [],
                    HashValid = false,
                });

                patchedCount++;

                log?.Invoke($"exefs 교체: {file.Name} ({file.Data.Length:N0} → {patchData.Length:N0} bytes)", LogLevel.Ok);
            }
            else if (ipsRef != null)
            {
                byte[] sourceData = file.Data;
                bool isCompressedCode = file.Name == ".code" && exHeader != null && exHeader.Length > ExHeaderCompressFlagOffset && (exHeader[ExHeaderCompressFlagOffset] & ExHeaderCompressFlagBit) != 0;

                if (isCompressedCode)
                    sourceData = BackwardLz77.Decompress(sourceData);

                byte[] diffData = await ipsRef.ReadSmallFileBytesAsync(ct);
                byte[] patchedData = await UniversalPatcher.ApplyPatchAsync(sourceData, diffData, null, ct);

                if (isCompressedCode)
                {
                    byte[] recompressed = BackwardLz77.Compress(patchedData);

                    if (recompressed.Length < patchedData.Length)
                        patchedData = recompressed;
                    else
                        exHeader![ExHeaderCompressFlagOffset] &= unchecked((byte)~ExHeaderCompressFlagBit);
                }

                patchedFiles.Add(new ExeFsFile
                {
                    Name = file.Name,
                    Data = patchedData,
                    ExpectedHash = [],
                    HashValid = false,
                });

                patchedCount++;

                log?.Invoke($"exefs IPS 패치 적용: {file.Name} ({sourceData.Length:N0} → {patchedData.Length:N0} bytes)", LogLevel.Ok);
            }
            else
                patchedFiles.Add(file);
        }

        return (Pack(patchedFiles), patchedCount);
    }

    private static (PatchFileRef? binRef, PatchFileRef? ipsRef) ResolvePatchFiles(string baseName, PatchFileIndex? exefsIndex, PatchFileIndex? rootIndex)
    {
        var bin = FindDirectChild(exefsIndex, baseName + ".bin", PatchFileKind.Overwrite) ?? FindDirectChild(rootIndex, baseName + ".bin", PatchFileKind.Overwrite);

        if (bin != null)
            return (bin, null);

        var diff = FindDirectChild(exefsIndex, baseName, PatchFileKind.BinaryPatch) ?? FindDirectChild(rootIndex, baseName, PatchFileKind.BinaryPatch);

        return diff != null ? (null, diff) : (null, null);
    }

    private static PatchFileRef? FindDirectChild(PatchFileIndex? index, string baseName, PatchFileKind kind)
    {
        return index?.Entries.FirstOrDefault(e => e.RelativeDir.Length == 0 && e.Kind == kind && string.Equals(e.BaseName, baseName, StringComparison.OrdinalIgnoreCase))?.File;
    }

    private static uint AlignUp(uint v, uint a) => (v + a - 1) & ~(a - 1);
}