using System.Buffers.Binary;
using PSP.PBP.AT3.Models;

namespace PSP.PBP.AT3.Services;

public static class WavWriter
{
    public static byte[] ToBytes(PcmData pcm)
    {
        int dataSize = pcm.Samples.Length * 2;
        var bytes = new byte[44 + dataSize];
        var s = bytes.AsSpan();

        "RIFF"u8.CopyTo(s[0..4]);
        BinaryPrimitives.WriteUInt32LittleEndian(s[4..8], (uint)(36 + dataSize));
        "WAVE"u8.CopyTo(s[8..12]);
        "fmt "u8.CopyTo(s[12..16]);
        BinaryPrimitives.WriteUInt32LittleEndian(s[16..20], 16);
        BinaryPrimitives.WriteUInt16LittleEndian(s[20..22], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(s[22..24], (ushort)pcm.Channels);
        BinaryPrimitives.WriteUInt32LittleEndian(s[24..28], (uint)pcm.SampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(s[28..32], (uint)(pcm.SampleRate * pcm.Channels * 2));
        BinaryPrimitives.WriteUInt16LittleEndian(s[32..34], (ushort)(pcm.Channels * 2));
        BinaryPrimitives.WriteUInt16LittleEndian(s[34..36], 16);
        "data"u8.CopyTo(s[36..40]);
        BinaryPrimitives.WriteUInt32LittleEndian(s[40..44], (uint)dataSize);

        for (int i = 0; i < pcm.Samples.Length; i++)
        {
            float v = Math.Clamp(pcm.Samples[i], -1.0f, 1.0f);

            BinaryPrimitives.WriteInt16LittleEndian(s[(44 + i * 2)..], (short)MathF.Round(v * 32767.0f));
        }

        return bytes;
    }
}