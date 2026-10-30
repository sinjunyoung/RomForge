using Common;
using LibHac.Tools.FsSystem;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using Path = System.IO.Path;

namespace RomForge.Core.Services.Switch;

internal static class XciWriter
{
    private const long XciHfs0HeaderSizePos = 0x138;
    private const long XciHfs0HeaderHashPos = 0x140;
    private const uint MediaSize = 0x200;

    private static readonly byte[] XciHeaderTemplate = [ 
        0x49, 0x04, 0x47, 0x85, 0x7D, 0x54, 0x74, 0xCA, 0xD5, 0x09, 0xB3, 0x6E, 0x81, 0xA9, 0xFE, 0x22, 0xAA, 0xBF, 0xE2, 0xA9, 0x78, 0xA3, 0x30, 0x1A, 0x9D, 0x0C, 0x2D, 0xEF, 0x27, 0xCF, 0x6D, 0x47,
        0x4D, 0xFE, 0x8A, 0xC2, 0xD5, 0xFD, 0xF4, 0x1D, 0xC8, 0xA2, 0xFA, 0xDF, 0xB6, 0xD2, 0xAD, 0x7D, 0x65, 0x6B, 0xEB, 0x63, 0x3B, 0x63, 0x8A, 0xE5, 0xE2, 0x5D, 0x21, 0xC1, 0x79, 0xAB, 0xCB, 0x1D,
        0x0C, 0x6B, 0x01, 0x6D, 0x29, 0xA6, 0xC8, 0x1F, 0xD5, 0xD2, 0x09, 0xAE, 0x3E, 0x67, 0x47, 0x71, 0x00, 0x73, 0xA3, 0xEA, 0xD9, 0x1A, 0x73, 0x15, 0xDC, 0x64, 0x34, 0xBF, 0xAE, 0xCD, 0x97, 0xD5,
        0x2E, 0x99, 0x4A, 0xBF, 0x72, 0xFE, 0x75, 0x04, 0x8A, 0x1E, 0x36, 0x65, 0x53, 0x79, 0x25, 0xEA, 0xB0, 0x07, 0x57, 0x16, 0xEC, 0x2A, 0x24, 0x11, 0xE0, 0x64, 0x76, 0x25, 0x1F, 0x35, 0xDF, 0x63,
        0xDB, 0xB4, 0xE2, 0x6C, 0xC3, 0x2F, 0x1A, 0xED, 0xB1, 0x01, 0x54, 0xEF, 0xA1, 0x21, 0xDF, 0xD7, 0x13, 0x21, 0xF1, 0xA1, 0xF2, 0xCF, 0x46, 0x0D, 0xD5, 0x68, 0x40, 0x6D, 0xC1, 0x4D, 0x4A, 0xDE,
        0x99, 0x08, 0x32, 0x8B, 0x81, 0x25, 0x50, 0xB4, 0xCD, 0x3C, 0x34, 0x1F, 0x61, 0xB8, 0x72, 0x44, 0x4B, 0x63, 0x84, 0x86, 0xEE, 0xCD, 0x9B, 0xC4, 0xB6, 0xD4, 0x7A, 0x71, 0x9F, 0x7E, 0xA7, 0x10,
        0xC9, 0xE9, 0xDB, 0x97, 0xCA, 0x2F, 0x15, 0xBC, 0xB9, 0x83, 0xEE, 0xBF, 0x1A, 0x5C, 0xFC, 0x4E, 0xFD, 0xF8, 0x9E, 0xF7, 0x11, 0xE3, 0xE1, 0x58, 0x9B, 0xED, 0x34, 0xBB, 0x82, 0xB5, 0xAB, 0x1D,
        0x7D, 0x20, 0x11, 0xDA, 0x33, 0x84, 0x8A, 0x1F, 0x5D, 0xAA, 0x42, 0xE7, 0x8D, 0x32, 0x7C, 0x6F, 0x13, 0xDD, 0xF9, 0x0D, 0xE3, 0x3B, 0x32, 0xB1, 0x6B, 0x6E, 0x7C, 0x9A, 0xB0, 0x43, 0x7B, 0x01,
        0x48, 0x45, 0x41, 0x44, 0x7B, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0xFA, 0x00, 0x00, 0x87, 0x50, 0xF4, 0xC0, 0xA9, 0xC5, 0xA9, 0x66, 0x11, 0x85, 0x0E, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x5B, 0x40, 0x8B, 0x14, 0x5E, 0x27, 0x7E, 0x81, 0xE5, 0xBF, 0x67, 0x7C, 0x94, 0x88, 0x8D, 0x7B, ];

    public static async Task WriteAsync(string displayName, string titleId, byte[] xciPrefixBuffer, List<(string Name, ulong AbsOffset, ulong Size, byte[] Hash, uint HashTargetSize)> rootEntriesTemplate, Stream? srcStream, 
        List<(string Name, Func<Stream, Action<long>, Task> Writer, long EstimatedSize, string Label)> fileEntries, Stream fout, IProgress<ProgressInfo>? progress, CancellationToken ct = default)
    {
        await fout.WriteAsync(xciPrefixBuffer, ct);

        long rootHeaderPos = fout.Position;
        var rootBuilderTemp = new Hfs0Builder();

        foreach (var (Name, AbsOffset, Size, Hash, HashTargetSize) in rootEntriesTemplate)
            rootBuilderTemp.AddFile(Name, 0, new byte[32], 0);

        int rootHeaderSize = (int)rootBuilderTemp.AlignedHeaderSize(MediaSize);

        await fout.WriteAsync(new byte[rootHeaderSize], ct);

        var rootEntryRelOffsets = new Dictionary<string, ulong>();
        var rootEntrySizes = new Dictionary<string, ulong>();
        var rootEntryHashes = new Dictionary<string, byte[]>();
        var rootEntryHashTargetSizes = new Dictionary<string, uint>();
        long rootDataStart = rootHeaderPos + rootHeaderSize;

        foreach (var (Name, AbsOffset, Size, Hash, HashTargetSize) in rootEntriesTemplate)
        {
            if (Name == "secure")
                continue;

            rootEntryRelOffsets[Name] = (ulong)(fout.Position - rootDataStart);
            rootEntrySizes[Name] = Size;
            rootEntryHashes[Name] = Hash;
            rootEntryHashTargetSizes[Name] = HashTargetSize;

            if (srcStream != null)
            {
                srcStream.Position = (long)AbsOffset;
                await Utils.CopyStreamAsync(srcStream, fout, (long)Size, null, ct);
            }
            else
                await fout.WriteAsync(BuildEmptyHfs0(), ct);
        }

        long secureAbsStart = fout.Position;

        rootEntryRelOffsets["secure"] = (ulong)(secureAbsStart - rootDataStart);

        var secureBuilderTemp = new Hfs0Builder();

        foreach (var (name, _, estimatedSize, _) in fileEntries)
            secureBuilderTemp.AddFile(name, (ulong)estimatedSize, new byte[32], MediaSize);

        int secureHeaderSize = (int)secureBuilderTemp.AlignedHeaderSize(MediaSize);

        await fout.WriteAsync(new byte[secureHeaderSize], ct);

        long secureDataStart = fout.Position;
        long totalEstimated = fileEntries.Sum(f => f.EstimatedSize);
        var reporter = new ProgressReporter(displayName, titleId, totalEstimated, progress);
        void onRead(long bytesRead) => reporter.AddProgress(bytesRead);
        var actualSizes = new ulong[fileEntries.Count];
        var actualHashes = new byte[fileEntries.Count][];
        string currentLabel = string.Empty;
        using var timer = new System.Timers.Timer(200);

        timer.Elapsed += (_, _) => reporter.ForceReport();
        timer.AutoReset = true;

        timer.Start();

        try
        {
            for (int i = 0; i < fileEntries.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var (_, writer, _, label) = fileEntries[i];

                currentLabel = label;

                long fileStartPos = fout.Position;
                using var hashStream = new HashTrackingStream(fout, MediaSize);

                await writer(hashStream, onRead);

                actualSizes[i] = (ulong)(fout.Position - fileStartPos);
                actualHashes[i] = hashStream.GetHash();
            }
        }
        finally
        {
            timer.Stop();
        }

        progress?.Report(new ProgressInfo(100, currentLabel, titleId, string.Empty, string.Empty));

        long finalEndPos = fout.Position;
        var secureBuilder = new Hfs0Builder();

        for (int i = 0; i < fileEntries.Count; i++)
            secureBuilder.AddFile(fileEntries[i].Name, actualSizes[i], actualHashes[i], MediaSize);

        byte[] secureHeader = secureBuilder.BuildHeader(MediaSize);
        byte[] secureHash = SHA256.HashData(secureHeader);
        ulong secureTotal = (ulong)(finalEndPos - secureAbsStart);

        fout.Position = secureAbsStart;

        await fout.WriteAsync(secureHeader, ct);

        var rootBuilder = new Hfs0Builder();

        foreach (var (Name, AbsOffset, Size, Hash, HashTargetSize) in rootEntriesTemplate)
        {
            string reName = Name;
            ulong relOffset = rootEntryRelOffsets[reName];

            if (reName == "secure")
                rootBuilder.AddFileWithOffset("secure", relOffset, secureTotal, secureHash, (uint)secureHeader.Length);
            else
                rootBuilder.AddFileWithOffset(reName, relOffset, rootEntrySizes[reName], rootEntryHashes[reName], rootEntryHashTargetSizes[reName]);
        }

        byte[] rootHeader = rootBuilder.BuildHeader(MediaSize);
        byte[] rootHash = SHA256.HashData(rootHeader);

        fout.Position = rootHeaderPos;

        await fout.WriteAsync(rootHeader, ct);
        BinaryPrimitives.WriteInt64LittleEndian(xciPrefixBuffer.AsSpan((int)XciHfs0HeaderSizePos), rootHeader.Length);
        rootHash.CopyTo(xciPrefixBuffer.AsSpan((int)XciHfs0HeaderHashPos));

        fout.Position = 0;

        await fout.WriteAsync(xciPrefixBuffer, ct);

        fout.Position = finalEndPos;

        await fout.FlushAsync(ct);
    }

    public static byte[] BuildEmptyHfs0() => new Hfs0Builder().BuildHeader(MediaSize);

    public static byte[] BuildPrefix(List<string> allPaths)
    {
        string? xciPath = allPaths.FirstOrDefault(p => Path.GetExtension(p).ToLowerInvariant() is ".xci" or ".xcz");

        if (xciPath != null)
        {
            using var f = File.OpenRead(xciPath);
            using var reader = new BinaryReader(f);

            f.Position = 0x130;

            long hfs0StartOffset = reader.ReadInt64();

            f.Position = 0;

            return reader.ReadBytes((int)hfs0StartOffset);
        }

        byte[] prefix = new byte[0x200];

        XciHeaderTemplate.CopyTo(prefix, 0);
        BinaryPrimitives.WriteInt64LittleEndian(prefix.AsSpan(0x130), 0x200);

        return prefix;
    }

    public static List<(string Name, ulong AbsOffset, ulong Size, byte[] Hash, uint HashTargetSize)> DummyRootEntries()
    {
        byte[] emptyHfs0 = BuildEmptyHfs0();
        byte[] emptyHash = SHA256.HashData(emptyHfs0);
        ulong emptySize = (ulong)emptyHfs0.Length;
        return [ ("normal", 0, emptySize, emptyHash, (uint)emptyHfs0.Length), ("update", emptySize, emptySize, emptyHash, (uint)emptyHfs0.Length), ("secure", 0, 0, new byte[32], 0), ];
    }
}