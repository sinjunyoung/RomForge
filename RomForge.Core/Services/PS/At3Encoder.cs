using System.IO;
using System.Text;

namespace RomForge.Core.Services.PS;

public static class At3Encoder
{
    private const int SampleRate = 44100;
    private const int Channels = 2;
    private const int BitrateKbps = 66;
    private const int BlockAlign = 192;

    public static byte[] ConvertWavToAt3(byte[] wavBytes)
    {
        using var msIn = new MemoryStream(wavBytes);
        using var reader = new BinaryReader(msIn);
        string chunkId = new(reader.ReadChars(4));

        if (chunkId != "RIFF")
            throw new InvalidDataException("올바른 RIFF WAV 파일이 아닙니다.");

        reader.ReadInt32();

        string format = new(reader.ReadChars(4));

        if (format != "WAVE")
            throw new InvalidDataException("올바른 WAVE 포맷이 아닙니다.");

        short numChannels = 0;
        int sampleRate = 0;
        short bitsPerSample = 0;
        byte[]? pcmData = null;

        while (msIn.Position < msIn.Length)
        {
            string subChunkId = new(reader.ReadChars(4));
            int subChunkSize = reader.ReadInt32();

            if (subChunkId == "fmt ")
            {
                short audioFormat = reader.ReadInt16();

                numChannels = reader.ReadInt16();
                sampleRate = reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt16();
                bitsPerSample = reader.ReadInt16();

                if (subChunkSize > 16)
                    reader.BaseStream.Seek(subChunkSize - 16, SeekOrigin.Current);
            }
            else if (subChunkId == "data")
            {
                pcmData = reader.ReadBytes(subChunkSize);
                break;
            }
            else
                reader.BaseStream.Seek(subChunkSize, SeekOrigin.Current);
        }

        if (pcmData == null)
            throw new InvalidDataException("WAV 파일 내 data 청크를 찾을 수 없습니다.");

        byte[] at3Payload = EncodePcmToAtrac3Payload(pcmData, numChannels, sampleRate, bitsPerSample);

        return BuildRiffAt3Header(at3Payload);
    }

    private static byte[] EncodePcmToAtrac3Payload(byte[] pcmData, short channels, int sampleRate, short bitsPerSample)
    {
        int totalBlocks = (pcmData.Length / (channels * (bitsPerSample / 8))) / 1024;

        if (totalBlocks == 0) 
            totalBlocks = 1;

        byte[] payload = new byte[totalBlocks * BlockAlign];
        int sourceOffset = 0;

        for (int i = 0; i < totalBlocks; i++)
        {
            int blockOffset = i * BlockAlign;

            payload[blockOffset] = 0x0E;
            payload[blockOffset + 1] = 0x00;

            int bytesToCopy = Math.Min(BlockAlign - 2, pcmData.Length - sourceOffset);

            if (bytesToCopy > 0)
            {
                Array.Copy(pcmData, sourceOffset, payload, blockOffset + 2, bytesToCopy);

                sourceOffset += bytesToCopy;
            }
        }

        return payload;
    }

    private static byte[] BuildRiffAt3Header(byte[] at3Payload)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        int fmtChunkSize = 32;
        int factChunkSize = 8;
        int riffDataSize = 4 + (8 + fmtChunkSize) + (8 + factChunkSize) + (8 + at3Payload.Length);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(riffDataSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(fmtChunkSize);
        writer.Write((short)0x0270);
        writer.Write((short)Channels);
        writer.Write(SampleRate);
        writer.Write(8250);
        writer.Write((short)BlockAlign);
        writer.Write((short)0);
        writer.Write((short)14);

        writer.Write((short)1);
        writer.Write(1024);
        writer.Write((short)0);
        writer.Write((short)1);
        writer.Write(1);
        writer.Write((short)0);

        writer.Write(Encoding.ASCII.GetBytes("fact"));
        writer.Write(factChunkSize);
        writer.Write(at3Payload.Length / BlockAlign * 1024);
        writer.Write(0);

        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(at3Payload.Length);
        writer.Write(at3Payload);

        writer.Flush();

        return ms.ToArray();
    }
}