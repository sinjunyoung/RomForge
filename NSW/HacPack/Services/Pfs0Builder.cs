using Common;
using NSW.HacPack.Models;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace NSW.HacPack.Services;

public static class Pfs0Builder
{
    private const uint MagicPfs0 = 0x30534650;
    public const int ExefsHashBlockSize = 0x10000;
    public const int LogoHashBlockSize = 0x1000;
    public const int MetaHashBlockSize = 0x1000;
    public const int PaddingSize = 0x200;

    public static void BuildFromMemoryStreams(IEnumerable<(string Name, Stream Stream)> fileStreams, Stream outputStream, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        var files = fileStreams.Select(f => (f.Name, f.Stream, Size: f.Stream.Length)).ToList();

        files.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

        if (files.Count == 0)
            throw new InvalidOperationException("No streams provided!");

        using var stringTableMs = new MemoryStream();
        var stringOffsets = new uint[files.Count];

        for (int i = 0; i < files.Count; i++)
        {
            stringOffsets[i] = (uint)stringTableMs.Position;

            byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(files[i].Name);

            stringTableMs.Write(nameBytes, 0, nameBytes.Length);
            stringTableMs.WriteByte(0);
        }

        ulong fullHeaderSize = (ulong)(Unsafe.SizeOf<Pfs0Header>() + Unsafe.SizeOf<Pfs0FileEntry>() * files.Count + stringTableMs.Length);
        ulong alignedHeaderSize = AlignUp(fullHeaderSize + 1, 0x20);
        ulong headerPadding = alignedHeaderSize - fullHeaderSize;
        var header = new Pfs0Header
        {
            Magic = MagicPfs0,
            NumFiles = (uint)files.Count,
            StringTableSize = (uint)(stringTableMs.Length + (long)headerPadding)
        };

        WriteStruct(outputStream, header);

        ulong fileDataRelOffset = 0;

        for (int i = 0; i < files.Count; i++)
        {
            var entry = new Pfs0FileEntry
            {
                Offset = fileDataRelOffset,
                Size = (ulong)files[i].Size,
                StringTableOffset = stringOffsets[i]
            };

            WriteStruct(outputStream, entry);

            fileDataRelOffset += (ulong)files[i].Size;
        }

        stringTableMs.Position = 0;

        stringTableMs.CopyTo(outputStream);

        if (headerPadding > 0)
        {
            Span<byte> pad = stackalloc byte[(int)headerPadding];

            pad.Clear();
            outputStream.Write(pad);
        }

        long totalSize = (long)fileDataRelOffset;
        const int bufSize = 0x800000;
        byte[] buf = ArrayPool<byte>.Shared.Rent(bufSize);
        var reporter = new ProgressReporter("PFS0 빌드 중", string.Empty, totalSize, progress);

        try
        {
            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();

                long remaining = file.Size;
                file.Stream.Position = 0;

                while (remaining > 0)
                {
                    int toRead = (int)Math.Min(bufSize, remaining);
                    int read = file.Stream.Read(buf, 0, toRead);

                    if (read <= 0)
                        break;

                    outputStream.Write(buf, 0, read);

                    remaining -= read;

                    reporter.AddProgress(read);
                }
            }

            reporter.ForceReport();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buf);
        }
    }

    public static void GeneratePfs0HashTable(Stream pfs0Stream, Stream hashTableStream, uint hashBlockSize, out ulong outHashTableSize, out ulong outPfs0Offset)
    {
        pfs0Stream.Position = 0;
        long srcFileSize = pfs0Stream.Length;
        byte[] buf = ArrayPool<byte>.Shared.Rent((int)hashBlockSize);
        Span<byte> hashSpan = stackalloc byte[32];

        try
        {
            long ofs = 0;

            while (ofs < srcFileSize)
            {
                int toRead = (int)Math.Min((long)hashBlockSize, srcFileSize - ofs);
                int read = pfs0Stream.Read(buf, 0, toRead);

                if (read != toRead) 
                    throw new IOException("Failed to read pfs0 stream");

                SHA256.HashData(buf.AsSpan(0, toRead), hashSpan);
                hashTableStream.Write(hashSpan);

                ofs += toRead;
            }

            outHashTableSize = (ulong)hashTableStream.Position;

            long paddingSize = PaddingSize - (long)outHashTableSize % PaddingSize;

            if (paddingSize != PaddingSize)
            {
                Span<byte> pad = stackalloc byte[(int)paddingSize];

                pad.Clear();
                hashTableStream.Write(pad);
            }
            outPfs0Offset = (ulong)hashTableStream.Position;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buf);
        }
    }

    private static ulong AlignUp(ulong value, ulong align) => (value + align - 1) & ~(align - 1);

    private static void WriteStruct<T>(Stream stream, in T value) where T : struct
    {
        Span<byte> buf = stackalloc byte[Unsafe.SizeOf<T>()];

        MemoryMarshal.Write(buf, in Unsafe.AsRef(in value));
        stream.Write(buf);
    }

    public static byte[] GetRootHash(Stream hashTableStream, ulong hashTableSize)
    {
        hashTableStream.Position = 0;

        using var sha = SHA256.Create();
        const int bufSize = 0x61A8000;
        byte[] buf = new byte[bufSize];
        ulong ofs = 0;

        while (ofs < hashTableSize)
        {
            ulong toRead = Math.Min(bufSize, hashTableSize - ofs);
            int read = hashTableStream.Read(buf, 0, (int)toRead);

            sha.TransformBlock(buf, 0, read, null, 0);

            ofs += (ulong)read;
        }

        sha.TransformFinalBlock([], 0, 0);

        return sha.Hash!;
    }
}