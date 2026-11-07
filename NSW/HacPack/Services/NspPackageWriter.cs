using NSW.HacPack.Models;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NSW.HacPack.Services;

public sealed class NspPackageWriter : IDisposable
{
    private sealed record PackedFile(string Name, long Offset, long Size);

    private const uint MagicPfs0 = 0x30534650;
    private const int NameSlotSize = 0x2A;
    private const long HeaderAlignment = 0x20;

    private readonly Stream _output;
    private readonly long _headerSize;
    private readonly List<PackedFile> _files = [];

    private bool _completed;

    public int MaxFiles { get; }

    public NczOptions? Ncz { get; set; }

    public NspPackageWriter(Stream output, int maxFiles)
    {
        if (!output.CanSeek || !output.CanRead || !output.CanWrite)
            throw new ArgumentException("출력 스트림은 읽기/쓰기/탐색이 모두 가능해야 합니다.", nameof(output));

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFiles, nameof(maxFiles));

        _output = output;
        MaxFiles = maxFiles;

        long rawHeaderSize = Unsafe.SizeOf<Pfs0Header>() + (long)Unsafe.SizeOf<Pfs0FileEntry>() * maxFiles + (long)NameSlotSize * maxFiles;

        _headerSize = AlignUp(rawHeaderSize + 1, HeaderAlignment);

        _output.SetLength(0);

        _output.Position = 0;

        _output.Write(new byte[_headerSize]);
    }

    public (string Name, byte[] Hash, long Size) AddNca(string extension, Func<Stream, (byte[] Hash, long Size)> write, bool compress = false)
    {
        if (_completed)
            throw new InvalidOperationException("이미 완료된 패키지입니다.");

        if (_files.Count >= MaxFiles)
            throw new InvalidOperationException($"패키지에 담을 수 있는 파일 수({MaxFiles})를 초과했습니다.");

        bool toNcz = compress && Ncz != null && extension == ".nca";
        long entryStart = _output.Length;
        long ncaStart = toNcz ? entryStart + NczInPlaceConverter.Gap : entryStart;

        _output.Position = ncaStart;

        try
        {
            var (hash, size) = write(_output);

            if (_output.Length != ncaStart + size)
                throw new InvalidOperationException($"NCA 크기가 일치하지 않습니다. 기록된 크기={_output.Length - ncaStart}, 보고된 크기={size}");

            long storedSize = size;

            if (toNcz)
            {
                storedSize = NczInPlaceConverter.Convert(_output, entryStart, ncaStart, size, Ncz!);

                Truncate(entryStart + storedSize);

                extension = ".ncz";
            }

            string name = Convert.ToHexString(hash, 0, 16).ToLowerInvariant() + extension;

            if (name.Length >= NameSlotSize)
                throw new InvalidOperationException($"파일명이 너무 깁니다: {name}");

            _files.Add(new PackedFile(name, entryStart - _headerSize, storedSize));

            _output.Position = entryStart + storedSize;

            return (name, hash, size);
        }
        catch
        {
            Truncate(entryStart);
            throw;
        }
    }

    public Checkpoint CreateCheckpoint() => new(_files.Count, _output.Length);

    public void Rollback(Checkpoint checkpoint)
    {
        if (_completed)
            throw new InvalidOperationException("이미 완료된 패키지입니다.");

        _files.RemoveRange(checkpoint.FileCount, _files.Count - checkpoint.FileCount);
        Truncate(checkpoint.Length);
    }

    public void Complete()
    {
        if (_completed)
            return;

        if (_files.Count == 0)
            throw new InvalidOperationException("NSP에 기록된 파일이 없습니다.");

        using var stringTable = new MemoryStream();
        var stringOffsets = new uint[_files.Count];

        for (int i = 0; i < _files.Count; i++)
        {
            stringOffsets[i] = (uint)stringTable.Position;

            byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(_files[i].Name);

            stringTable.Write(nameBytes);
            stringTable.WriteByte(0);
        }

        long entriesEnd = Unsafe.SizeOf<Pfs0Header>() + (long)Unsafe.SizeOf<Pfs0FileEntry>() * _files.Count;
        long stringTableSize = _headerSize - entriesEnd;

        if (stringTable.Length > stringTableSize)
            throw new InvalidOperationException("파일명 테이블이 예약된 헤더 공간을 초과했습니다.");

        long endPosition = _output.Length;

        _output.Position = 0;

        WriteStruct(_output, new Pfs0Header
        {
            Magic = MagicPfs0,
            NumFiles = (uint)_files.Count,
            StringTableSize = (uint)stringTableSize
        });

        for (int i = 0; i < _files.Count; i++)
        {
            WriteStruct(_output, new Pfs0FileEntry
            {
                Offset = (ulong)_files[i].Offset,
                Size = (ulong)_files[i].Size,
                StringTableOffset = stringOffsets[i]
            });
        }

        stringTable.Position = 0;

        stringTable.CopyTo(_output);

        long padding = stringTableSize - stringTable.Length;

        if (padding > 0)
            _output.Write(new byte[padding]);

        _output.Position = endPosition;

        _output.Flush();

        _completed = true;
    }

    public void Dispose() => _output.Dispose();

    private void Truncate(long length)
    {
        _output.SetLength(length);
        _output.Position = length;
    }

    private static long AlignUp(long value, long align) => (value + align - 1) / align * align;

    private static void WriteStruct<T>(Stream stream, T value) where T : struct
    {
        Span<byte> buffer = stackalloc byte[Unsafe.SizeOf<T>()];

        MemoryMarshal.Write(buffer, in value);
        stream.Write(buffer);
    }
}