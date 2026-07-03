using _3DS.Core.Models;
using Patch.Core.Formats;
using System.Security.Cryptography;
using System.Text;

namespace _3DS.Core.Services;

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

    public static async Task<byte[]> PackWithPatchAsync(IReadOnlyList<ExeFsFile> originalFiles, string? exefsPatchDir, byte[]? exHeader, string? patchRootDir, CancellationToken ct = default)
    {
        bool hasExefsDir = exefsPatchDir != null && Directory.Exists(exefsPatchDir);
        bool hasRootFallback = !string.IsNullOrEmpty(patchRootDir);

        if (!hasExefsDir && !hasRootFallback)
            return Pack(originalFiles);

        var patchedFiles = new List<ExeFsFile>();

        foreach (var file in originalFiles)
        {
            string baseName = file.Name == ".code" ? "code" : file.Name;
            bool allowRootFallback = file.Name == ".code";

            var (binPath, ipsPath) = ResolvePatchFiles(baseName, hasExefsDir ? exefsPatchDir : null, patchRootDir, allowRootFallback);

            if (binPath != null)
            {
                byte[] patchData = await File.ReadAllBytesAsync(binPath, ct);

                patchedFiles.Add(new ExeFsFile
                {
                    Name = file.Name,
                    Data = patchData,
                    ExpectedHash = [],
                    HashValid = false,
                });
            }
            else if (ipsPath != null)
            {
                byte[] sourceData = file.Data;
                bool isCompressedCode = file.Name == ".code" && exHeader != null && exHeader.Length > ExHeaderCompressFlagOffset && (exHeader[ExHeaderCompressFlagOffset] & ExHeaderCompressFlagBit) != 0;

                if (isCompressedCode)
                    sourceData = BackwardLz77.Decompress(sourceData);

                byte[] ipsData = await File.ReadAllBytesAsync(ipsPath, ct);
                byte[] patchedData = await Ips.ApplyPatchAsync(sourceData, ipsData, null, ct);

                if (isCompressedCode)
                    exHeader![ExHeaderCompressFlagOffset] &= unchecked((byte)~ExHeaderCompressFlagBit);

                patchedFiles.Add(new ExeFsFile
                {
                    Name = file.Name,
                    Data = patchedData,
                    ExpectedHash = [],
                    HashValid = false,
                });
            }
            else
            {
                patchedFiles.Add(file);
            }
        }

        return Pack(patchedFiles);
    }

    private static (string? binPath, string? ipsPath) ResolvePatchFiles(string baseName, string? exefsPatchDir, string? patchRootDir, bool allowRootFallback)
    {
        if (exefsPatchDir != null)
        {
            string bin = Path.Combine(exefsPatchDir, baseName + ".bin");
            string ips = Path.Combine(exefsPatchDir, baseName + ".ips");

            if (File.Exists(bin)) return (bin, null);
            if (File.Exists(ips)) return (null, ips);
        }

        if (allowRootFallback && !string.IsNullOrEmpty(patchRootDir))
        {
            string bin = Path.Combine(patchRootDir, baseName + ".bin");
            string ips = Path.Combine(patchRootDir, baseName + ".ips");

            if (File.Exists(bin)) return (bin, null);
            if (File.Exists(ips)) return (null, ips);
        }

        return (null, null);
    }

    private static uint AlignUp(uint v, uint a) => (v + a - 1) & ~(a - 1);
}