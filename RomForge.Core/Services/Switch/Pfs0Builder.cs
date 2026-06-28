using Common;
using RomForge.Core.Models.Switch;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RomForge.Core.Services.Switch;

public static class Pfs0Builder
{
    private const uint MagicPfs0 = 0x30534650;

    public static async Task WriteAsync(string displayName, string titleId, IEnumerable<(string Name, Func<Stream, Action<long>, Task> Writer, long EstimatedSize, string Label)> files, Stream outputStream, uint alignment = 0x20, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        if (!outputStream.CanSeek)
            throw new InvalidOperationException("outputStream must be seekable");

        var fileList = files
                .OrderBy(f => f.Name, StringComparer.Ordinal)
                .ToList(); var stringTable = new List<byte>();
        var stringOffsets = new List<uint>();

        foreach (var (name, _, _, _) in fileList)
        {
            stringOffsets.Add((uint)stringTable.Count);
            stringTable.AddRange(System.Text.Encoding.UTF8.GetBytes(name));
            stringTable.Add(0);
        }

        ulong fullHeaderSize = (ulong)(Marshal.SizeOf<Pfs0Header>() + Marshal.SizeOf<Pfs0FileEntry>() * fileList.Count + stringTable.Count);
        ulong alignedHeaderSize = AlignUp(fullHeaderSize + 1, alignment);
        ulong headerPadding = alignedHeaderSize - fullHeaderSize;

        var header = new Pfs0Header
        {
            Magic = MagicPfs0,
            NumFiles = (uint)fileList.Count,
            StringTableSize = (uint)(stringTable.Count + (int)headerPadding)
        };

        WriteStruct(outputStream, header);

        long entryTablePos = outputStream.Position;

        for (int i = 0; i < fileList.Count; i++)
        {
            WriteStruct(outputStream, new Pfs0FileEntry
            {
                Offset = 0,
                Size = 0,
                StringTableOffset = stringOffsets[i]
            });
        }

        outputStream.Write(stringTable.ToArray());
        outputStream.Write(new byte[headerPadding]);

        long totalEstimated = fileList.Sum(f => f.EstimatedSize);
        string currentLabel = string.Empty;
        var reporter = new ProgressReporter(displayName, titleId, totalEstimated, progress);

        void onRead(long bytesRead) => reporter.AddProgress(bytesRead);

        var actualOffsets = new ulong[fileList.Count];
        var actualSizes = new ulong[fileList.Count];
        ulong relOffset = 0;
        using var timer = new System.Timers.Timer(200);

        timer.Elapsed += (_, _) => reporter.ForceReport();
        timer.AutoReset = true;
        timer.Start();

        try
        {
            for (int i = 0; i < fileList.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var (name, writer, _, label) = fileList[i];

                currentLabel = label;
                actualOffsets[i] = relOffset;
                long fileStartPos = outputStream.Position;

                await writer(outputStream, onRead);

                ulong written = (ulong)(outputStream.Position - fileStartPos);
                actualSizes[i] = written;
                relOffset += written;
            }
        }
        finally
        {
            timer.Stop();
        }

        progress?.Report(new ProgressInfo(100, currentLabel, titleId, string.Empty, string.Empty));

        long endPos = outputStream.Position;
        outputStream.Position = entryTablePos;

        for (int i = 0; i < fileList.Count; i++)
        {
            WriteStruct(outputStream, new Pfs0FileEntry
            {
                Offset = actualOffsets[i],
                Size = actualSizes[i],
                StringTableOffset = stringOffsets[i]
            });
        }

        outputStream.Position = endPos;
        await outputStream.FlushAsync(ct);
    }

    public static uint GetAlignmentPadding(string inputPath)
    {
        uint detectedAlignment = 0x20;

        using (var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var reader = new BinaryReader(fs))
        {
            if (fs.Length >= 0x10)
            {
                uint magic = reader.ReadUInt32();
                if (magic == MagicPfs0)
                {
                    uint numFiles = reader.ReadUInt32();
                    uint stringTableSize = reader.ReadUInt32();

                    ulong headerStructsSize = 0x10 + ((ulong)numFiles * 0x18);
                    ulong totalHeaderSizeWithPadding = headerStructsSize + stringTableSize;

                    if (totalHeaderSizeWithPadding % 0x20 == 0)
                        detectedAlignment = 0x20;
                    else if (totalHeaderSizeWithPadding % 0x10 == 0)
                        detectedAlignment = 0x10;
                }
            }
        }

        return detectedAlignment;
    }

    private static ulong AlignUp(ulong value, ulong align) => value + align - 1 & ~(align - 1);

    private static void WriteStruct<T>(Stream stream, T value) where T : struct
    {
        Span<byte> buffer = stackalloc byte[Unsafe.SizeOf<T>()];
        MemoryMarshal.Write(buffer, in value);
        stream.Write(buffer);
    }
}