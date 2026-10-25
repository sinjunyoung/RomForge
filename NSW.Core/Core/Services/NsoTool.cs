using K4os.Compression.LZ4;
using LibHac.Util;
using System.IO;
using System.Security.Cryptography;

namespace NSW.Core.Services;

public static class NsoTool
{
    private const int HeaderSize = 0x100;

    private const int MinHeaderReadSize = 0x60;

    public static bool TryReadHeaderInfo(string path, out bool isNso, out string buildIdHex)
    {
        isNso = false;
        buildIdHex = string.Empty;

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        if (fs.Length < MinHeaderReadSize)
            return false;

        Span<byte> header = stackalloc byte[MinHeaderReadSize];
        int totalRead = 0;

        while (totalRead < MinHeaderReadSize)
        {
            int read = fs.Read(header[totalRead..]);

            if (read == 0)
                return false;

            totalRead += read;
        }

        if (!(header[0] == (byte)'N' && header[1] == (byte)'S' && header[2] == (byte)'O' && header[3] == (byte)'0'))
            return false;

        isNso = true;
        buildIdHex = Convert.ToHexString(header[0x40..0x60]).TrimEnd('0');

        return true;
    }

    private record struct SegInfo(int FileOffField, int MemOffField, int SizeField, int FileSizeField, int CompBit, int HashBit, int HashOffset);

    private static readonly SegInfo[] Segs = [ new(0x10, 0x14, 0x18, 0x60, 0, 3, 0xA0), new(0x20, 0x24, 0x28, 0x64, 1, 4, 0xC0), new(0x30, 0x34, 0x38, 0x68, 2, 5, 0xE0), ];

    public static bool IsNso(byte[] data) => data.Length >= 4 && data[0] == 'N' && data[1] == 'S' && data[2] == 'O' && data[3] == '0';

    public static string GetBuildIdHex(byte[] nso) => Convert.ToHexString(nso.AsSpan(0x40, 32)).TrimEnd('0');

    public static bool IsCompressed(byte[] nso)
    {
        uint flags = BitConverter.ToUInt32(nso, 0x0C);
        return Segs.Any(s => (flags & (1u << s.CompBit)) != 0);
    }

    public static byte[] DecompressToPlain(byte[] nso)
    {
        if (!IsNso(nso))
            throw new InvalidDataException("유효한 NSO 파일이 아닙니다.");

        uint flags = BitConverter.ToUInt32(nso, 0x0C);
        var segData = new byte[3][];

        for (int i = 0; i < 3; i++)
        {
            var s = Segs[i];
            int fileOff = BitConverter.ToInt32(nso, s.FileOffField);
            int memSize = BitConverter.ToInt32(nso, s.SizeField);
            bool compressed = (flags & (1u << s.CompBit)) != 0;
            int fileSize = compressed ? BitConverter.ToInt32(nso, s.FileSizeField) : memSize;
            byte[] raw = new byte[fileSize];

            Array.Copy(nso, fileOff, raw, 0, fileSize);

            segData[i] = compressed ? Lz4.Decompress(raw, memSize) : raw;
        }

        byte[] outNso = new byte[HeaderSize + segData.Sum(s => s.Length)];

        Array.Copy(nso, 0, outNso, 0, HeaderSize);

        uint newFlags = flags;
        int cursor = HeaderSize;

        for (int i = 0; i < 3; i++)
        {
            var s = Segs[i];

            Array.Copy(segData[i], 0, outNso, cursor, segData[i].Length);
            BitConverter.GetBytes(cursor).CopyTo(outNso, s.FileOffField);
            BitConverter.GetBytes(segData[i].Length).CopyTo(outNso, s.FileSizeField);

            newFlags &= ~(1u << s.CompBit);

            if ((flags & (1u << s.HashBit)) != 0)
                SHA256.HashData(segData[i]).CopyTo(outNso, s.HashOffset);

            cursor += segData[i].Length;
        }

        BitConverter.GetBytes(newFlags).CopyTo(outNso, 0x0C);

        return outNso;
    }

    public static byte[] RecompressFromPlain(byte[] plain, uint originalFlags)
    {
        var segData = new byte[3][];
        var segCompressed = new byte[3][];

        for (int i = 0; i < 3; i++)
        {
            var s = Segs[i];
            int fileOff = BitConverter.ToInt32(plain, s.FileOffField);
            int size = BitConverter.ToInt32(plain, s.SizeField);
            byte[] raw = new byte[size];

            Array.Copy(plain, fileOff, raw, 0, size);

            segData[i] = raw;

            bool wasCompressed = (originalFlags & (1u << s.CompBit)) != 0;

            if (!wasCompressed)
            {
                segCompressed[i] = raw;
                continue;
            }

            int maxLen = LZ4Codec.MaximumOutputSize(raw.Length);
            byte[] buffer = new byte[maxLen];
            int encodedLen = LZ4Codec.Encode(raw, 0, raw.Length, buffer, 0, maxLen, LZ4Level.L09_HC);

            if (encodedLen <= 0)
                throw new InvalidOperationException($"세그먼트 {i} LZ4 압축 실패 (encodedLen={encodedLen})");

            byte[] compressed = new byte[encodedLen];

            Array.Copy(buffer, compressed, encodedLen);

            segCompressed[i] = compressed;
        }

        byte[] outNso = new byte[HeaderSize + segCompressed.Sum(s => s.Length)];

        Array.Copy(plain, 0, outNso, 0, HeaderSize);

        int cursor = HeaderSize;

        for (int i = 0; i < 3; i++)
        {
            var s = Segs[i];

            Array.Copy(segCompressed[i], 0, outNso, cursor, segCompressed[i].Length);
            BitConverter.GetBytes(cursor).CopyTo(outNso, s.FileOffField);
            BitConverter.GetBytes(segData[i].Length).CopyTo(outNso, s.SizeField);
            BitConverter.GetBytes(segCompressed[i].Length).CopyTo(outNso, s.FileSizeField);

            if ((originalFlags & (1u << s.HashBit)) != 0)
                SHA256.HashData(segData[i]).CopyTo(outNso, s.HashOffset);

            cursor += segCompressed[i].Length;
        }

        BitConverter.GetBytes(originalFlags).CopyTo(outNso, 0x0C);

        if (outNso.Length != cursor)
            Array.Resize(ref outNso, cursor);

        return outNso;
    }
}