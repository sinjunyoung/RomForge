using System.Buffers.Binary;

namespace PSP.PBP.AT3.Services;

public static class Header
{
    public const int SamplesPerFrame = 1024;

    public const int SampleRate = 44100;

    public const long LoopMinSamples = 0x8000;

    private const ushort FormatTagAtrac3 = 0x0270;
    private const int ExtraDataSize = 14;
    private const int FmtChunkSize = 18 + ExtraDataSize;
    private const int PcmBytesPerFrame = 0x1000;
    private const int HeaderSizeNoLoop = 76;
    private const int HeaderSizeLoop = 144;
    private const int LoopStart = 0x800;
    private const int LoopTrim = 0x2000;
    private const int LoopEndGuard = 0x2801;
    private const int SamplePeriodNs = 22676;
    private const int MidiUnityNote = 60;

    private static readonly byte[] ListChunk = BuildListChunk();

    public static bool UsesLoop(long totalSamples) => totalSamples > LoopMinSamples;

    public static int HeaderSize(bool loop) => loop ? HeaderSizeLoop : HeaderSizeNoLoop;

    private static uint FactSamples(long totalSamples, bool loop) => loop ? (uint)((totalSamples & ~0xFFFL) - LoopTrim) : (uint)totalSamples;

    public static byte[] Build(int channels, int frameSize, bool jointStereo, long totalSamples)
    {
        bool loop = UsesLoop(totalSamples);

        var h = new byte[HeaderSize(loop)];
        var s = h.AsSpan();

        "RIFF"u8.CopyTo(s[0..4]);
        "WAVE"u8.CopyTo(s[8..12]);
        "fmt "u8.CopyTo(s[12..16]);

        BinaryPrimitives.WriteUInt32LittleEndian(s[16..20], FmtChunkSize);
        BinaryPrimitives.WriteUInt16LittleEndian(s[20..22], FormatTagAtrac3);
        BinaryPrimitives.WriteUInt16LittleEndian(s[22..24], (ushort)channels);
        BinaryPrimitives.WriteUInt32LittleEndian(s[24..28], SampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(s[28..32], (uint)((ulong)frameSize * SampleRate / SamplesPerFrame));
        BinaryPrimitives.WriteUInt16LittleEndian(s[32..34], (ushort)frameSize);
        BinaryPrimitives.WriteUInt16LittleEndian(s[34..36], 0);
        BinaryPrimitives.WriteUInt16LittleEndian(s[36..38], ExtraDataSize);

        ushort mode = (ushort)(jointStereo ? 1 : 0);

        BinaryPrimitives.WriteUInt16LittleEndian(s[38..40], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(s[40..44], PcmBytesPerFrame);
        BinaryPrimitives.WriteUInt16LittleEndian(s[44..46], mode);
        BinaryPrimitives.WriteUInt16LittleEndian(s[46..48], mode);
        BinaryPrimitives.WriteUInt16LittleEndian(s[48..50], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(s[50..52], 0);

        "fact"u8.CopyTo(s[52..56]);

        BinaryPrimitives.WriteUInt32LittleEndian(s[56..60], 8);
        BinaryPrimitives.WriteUInt32LittleEndian(s[60..64], FactSamples(totalSamples, loop));
        BinaryPrimitives.WriteUInt32LittleEndian(s[64..68], LoopStart);

        int dataHeader = 68;

        if (loop)
        {
            "smpl"u8.CopyTo(s[68..72]);

            BinaryPrimitives.WriteUInt32LittleEndian(s[72..76], 60);
            BinaryPrimitives.WriteUInt32LittleEndian(s[84..88], SamplePeriodNs);
            BinaryPrimitives.WriteUInt32LittleEndian(s[88..92], MidiUnityNote);
            BinaryPrimitives.WriteUInt32LittleEndian(s[104..108], 1);
            BinaryPrimitives.WriteUInt32LittleEndian(s[108..112], 24);
            BinaryPrimitives.WriteUInt32LittleEndian(s[120..124], LoopStart);
            BinaryPrimitives.WriteUInt32LittleEndian(s[124..128], FactSamples(totalSamples, true) - LoopEndGuard);
            dataHeader = 136;
        }

        "data"u8.CopyTo(s[dataHeader..(dataHeader + 4)]);

        return h;
    }

    public static void Finish(Stream stream, ulong framesWritten, int frameSize, long totalSamples)
    {
        bool loop = UsesLoop(totalSamples);
        ulong dataSize = framesWritten * (ulong)frameSize;
        ulong fileSize = (ulong)HeaderSize(loop) + dataSize;

        if (fileSize + 64 >= uint.MaxValue)
            return;

        stream.Seek(0, SeekOrigin.End);

        if ((dataSize & 1) != 0)
            stream.WriteByte(0);

        stream.Write(ListChunk, 0, ListChunk.Length);

        long riffSize = stream.Position - 8;

        Span<byte> buf = stackalloc byte[4];

        BinaryPrimitives.WriteUInt32LittleEndian(buf, (uint)riffSize);
        stream.Seek(4, SeekOrigin.Begin);
        stream.Write(buf);

        BinaryPrimitives.WriteUInt32LittleEndian(buf, (uint)dataSize);
        stream.Seek(HeaderSize(loop) - 4, SeekOrigin.Begin);
        stream.Write(buf);

        stream.Seek(0, SeekOrigin.End);
    }

    private static byte[] BuildListChunk()
    {
        var text = "ATRACDENC\0"u8;
        var b = new byte[8 + 4 + 4 + 4 + text.Length];
        var s = b.AsSpan();

        "LIST"u8.CopyTo(s[0..4]);

        BinaryPrimitives.WriteUInt32LittleEndian(s[4..8], (uint)(b.Length - 8));

        "INFO"u8.CopyTo(s[8..12]);
        "ISFT"u8.CopyTo(s[12..16]);

        BinaryPrimitives.WriteUInt32LittleEndian(s[16..20], (uint)text.Length);
        text.CopyTo(s[20..]);

        return b;
    }
}