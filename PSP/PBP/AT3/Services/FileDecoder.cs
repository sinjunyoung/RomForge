using System.Buffers.Binary;
using PSP.PBP.AT3.Models;

namespace PSP.PBP.AT3.Services;

public static class FileDecoder
{
    private const ushort FormatTagAtrac3 = 0x0270;
    private const int DefaultSkipSamples = 0x800;

    public static PcmData Decode(byte[] file, Action<double>? progress = null)
    {
        if (file == null || file.Length < 12 || !file.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !file.AsSpan(8, 4).SequenceEqual("WAVE"u8))
            throw new InvalidDataException("AT3 파일이 아닙니다.");

        int channels = 0;
        int sampleRate = 0;
        int frameSize = 0;
        bool jointStereo = false;
        bool hasFormat = false;
        long factSamples = -1;
        long skipSamples = DefaultSkipSamples;
        int dataStart = -1;
        long dataSize = 0;
        int pos = 12;

        while (pos + 8 <= file.Length)
        {
            var id = file.AsSpan(pos, 4);
            long size = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(pos + 4, 4));
            int body = pos + 8;

            if (id.SequenceEqual("fmt "u8))
            {
                if (size < 18 || body + 18 > file.Length)
                    throw new InvalidDataException("fmt 청크가 올바르지 않습니다.");

                if (BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(body, 2)) != FormatTagAtrac3)
                    throw new NotSupportedException("ATRAC3 포맷이 아닙니다.");

                channels = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(body + 2, 2));
                sampleRate = (int)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(body + 4, 4));
                frameSize = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(body + 12, 2));

                if (size >= 32 && body + 32 <= file.Length)
                    jointStereo = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(body + 24, 2)) == 1;

                hasFormat = true;
            }
            else if (id.SequenceEqual("fact"u8))
            {
                if (size >= 4 && body + 4 <= file.Length)
                    factSamples = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(body, 4));

                if (size >= 8 && body + 8 <= file.Length)
                    skipSamples = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(body + 4, 4));
            }
            else if (id.SequenceEqual("data"u8))
            {
                dataStart = body;
                dataSize = Math.Min(size, file.Length - body);
                break;
            }

            pos = (int)Math.Min(file.Length, body + size + (size & 1));
        }

        if (!hasFormat || dataStart < 0)
            throw new InvalidDataException("AT3 파일 구조가 올바르지 않습니다.");

        if (sampleRate != CodecInfo.SampleRateHz)
            throw new NotSupportedException("지원하지 않는 샘플레이트입니다.");

        if (frameSize <= 0)
            throw new InvalidDataException("AT3 프레임 크기가 올바르지 않습니다.");

        long frames = dataSize / frameSize;

        if (frames == 0)
            throw new InvalidDataException("AT3에 오디오 데이터가 없습니다.");

        var decoder = new Decoder(channels, frameSize, jointStereo);
        var samples = new float[frames * CodecInfo.NumSamples * channels];
        var frameBuffer = new float[CodecInfo.NumSamples * channels];

        for (long f = 0; f < frames; f++)
        {
            decoder.DecodeFrame(file, dataStart + (int)(f * frameSize), frameBuffer);
            frameBuffer.CopyTo(samples, f * frameBuffer.Length);
            progress?.Invoke((double)(f + 1) / frames);
        }

        long skip = Math.Min(skipSamples, frames * CodecInfo.NumSamples);
        long length = frames * CodecInfo.NumSamples - skip;

        if (factSamples >= 0)
            length = Math.Min(length, factSamples);

        return new PcmData
        {
            Channels = channels,
            SampleRate = sampleRate,
            Samples = samples.AsSpan((int)(skip * channels), (int)(length * channels)).ToArray()
        };
    }
}