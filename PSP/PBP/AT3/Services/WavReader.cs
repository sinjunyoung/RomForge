using PSP.PBP.AT3.Models;
using System.Buffers.Binary;

namespace PSP.PBP.AT3.Services;

public static class WavReader
{
    private sealed class FormatInfo
    {
        public int Tag;

        public int Channels;

        public int SampleRate;

        public int BlockAlign;

        public int Bits;

        public uint ChannelMask;

        public int SamplesPerBlock;

        public short[] CoefPairs = [];
    }

    public static PcmData Read(string path)
    {
        using var fs = File.OpenRead(path);
        return Read(fs);
    }

    public static PcmData Read(Stream stream)
    {
        using var br = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        string riff = new(br.ReadChars(4));

        if (riff == "RF64" || riff == "BW64")
            throw new NotSupportedException("RF64 files (larger than 4 GB) are not supported");

        if (riff == "RIFX")
            throw new NotSupportedException("Big-endian RIFX files are not supported");

        if (riff != "RIFF")
            throw new InvalidDataException("Not a RIFF/WAVE file");

        br.ReadUInt32();

        if (new string(br.ReadChars(4)) != "WAVE")
            throw new InvalidDataException("Not a WAVE file");

        FormatInfo? fmt = null;
        byte[]? data = null;

        while (stream.Position + 8 <= stream.Length)
        {
            string id = new(br.ReadChars(4));
            uint size = br.ReadUInt32();
            long next = stream.Position + size + (size & 1);

            if (id == "fmt ")
                fmt = ParseFormat(br.ReadBytes((int)Math.Min(size, 4096u)));
            else if (id == "data")
            {
                long available = Math.Min((long)size, stream.Length - stream.Position);

                if (size == 0 || size == uint.MaxValue)
                    available = stream.Length - stream.Position;

                data = br.ReadBytes((int)available);
                next = stream.Position;
            }

            if (next > stream.Length)
                break;

            stream.Position = next;
        }

        if (fmt == null || data == null)
            throw new InvalidDataException("Missing fmt or data chunk");

        if (fmt.Channels < 1)
            throw new InvalidDataException("Invalid channel count");

        float[] samples = Decode(fmt, data);
        int channels = fmt.Channels;

        if (channels > 2)
        {
            samples = ChannelMixer.ToStereo(samples, channels, fmt.ChannelMask);
            channels = 2;
        }

        if (samples.Length == 0)
            throw new InvalidDataException("The WAV file contains no audio data");

        return new PcmData { Channels = channels, SampleRate = fmt.SampleRate, Samples = samples };
    }

    private static FormatInfo ParseFormat(byte[] b)
    {
        if (b.Length < 16)
            throw new InvalidDataException("fmt chunk is too short");

        var f = new FormatInfo
        {
            Tag = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(0)),
            Channels = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(2)),
            SampleRate = (int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(4)),
            BlockAlign = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(12)),
            Bits = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(14))
        };

        int cbSize = b.Length >= 18 ? BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(16)) : 0;
        int extraStart = 18;
        int extraLen = Math.Max(0, Math.Min(cbSize, b.Length - extraStart));

        if (f.Tag == 0xFFFE && extraLen >= 22)
        {
            f.ChannelMask = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(extraStart + 2));
            f.Tag = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(extraStart + 6));
        }
        else if (f.Tag == 0x0011 && extraLen >= 2)
            f.SamplesPerBlock = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(extraStart));
        else if (f.Tag == 0x0002 && extraLen >= 4)
        {
            f.SamplesPerBlock = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(extraStart));

            int numCoef = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(extraStart + 2));

            numCoef = Math.Min(numCoef, (extraLen - 4) / 4);

            f.CoefPairs = new short[numCoef * 2];

            for (int i = 0; i < f.CoefPairs.Length; i++)
                f.CoefPairs[i] = BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(extraStart + 4 + i * 2));
        }

        return f;
    }

    private static float[] Decode(FormatInfo f, byte[] data)
    {
        switch (f.Tag)
        {
            case 1:
                if (f.Bits != 8 && f.Bits != 16 && f.Bits != 24 && f.Bits != 32)
                    throw new NotSupportedException($"{f.Bits}-bit PCM is not supported");

                return DecodePcm(f, data);
            case 3:
                if (f.Bits != 32 && f.Bits != 64)
                    throw new NotSupportedException($"{f.Bits}-bit float is not supported");

                return DecodePcm(f, data);
            case 6:
            case 7:
                return DecodeG711(f.Tag == 6, f.Channels, data);
            case 2:
                return AdpcmDecoder.DecodeMs(f.Channels, f.BlockAlign, f.CoefPairs, data);
            case 0x11:
                return AdpcmDecoder.DecodeIma(f.Channels, f.BlockAlign, data);
            default:
                throw new NotSupportedException($"Unsupported WAV format (tag 0x{f.Tag:X4})");
        }
    }

    private static float[] DecodePcm(FormatInfo f, byte[] data)
    {
        int bytesPerSample = f.Bits / 8;
        int count = data.Length / bytesPerSample / f.Channels * f.Channels;
        var samples = new float[count];
        bool isFloat = f.Tag == 3;

        for (int i = 0; i < count; i++)
        {
            int o = i * bytesPerSample;

            samples[i] = (isFloat, f.Bits) switch
            {
                (false, 8) => (data[o] - 128) / 128.0f,
                (false, 16) => BitConverter.ToInt16(data, o) / 32768.0f,
                (false, 24) => (((data[o] << 8) | (data[o + 1] << 16) | (data[o + 2] << 24)) >> 8) / 8388608.0f,
                (false, 32) => BitConverter.ToInt32(data, o) / 2147483648.0f,
                (true, 32) => BitConverter.ToSingle(data, o),
                _ => (float)BitConverter.ToDouble(data, o)
            };
        }

        return samples;
    }

    private static float[] DecodeG711(bool aLaw, int channels, byte[] data)
    {
        int count = data.Length / channels * channels;
        var samples = new float[count];

        for (int i = 0; i < count; i++)
            samples[i] = (aLaw ? ALaw(data[i]) : MuLaw(data[i])) / 32768.0f;

        return samples;
    }

    private static short ALaw(byte a)
    {
        a ^= 0x55;

        int t = (a & 0x0F) << 4;
        int seg = (a & 0x70) >> 4;

        switch (seg)
        {
            case 0:
                t += 8;
                break;
            case 1:
                t += 0x108;
                break;
            default:
                t += 0x108;
                t <<= seg - 1;
                break;
        }

        return (short)((a & 0x80) != 0 ? t : -t);
    }

    private static short MuLaw(byte u)
    {
        u = (byte)~u;

        int t = ((u & 0x0F) << 3) + 0x84;

        t <<= (u & 0x70) >> 4;

        return (short)((u & 0x80) != 0 ? 0x84 - t : t - 0x84);
    }
}