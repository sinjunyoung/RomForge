using NLayer;
using PSP.PBP.AT3.Models;

namespace PSP.PBP.AT3.Services;

public static class Mp3Reader
{
    public static PcmData Read(string path)
    {
        using var fs = File.OpenRead(path);
        return Read(fs);
    }

    public static PcmData Read(Stream stream, int maxSeconds = 0)
    {
        using var mpeg = new MpegFile(stream);
        int channels = mpeg.Channels;
        int sampleRate = mpeg.SampleRate;

        if (channels != 1 && channels != 2)
            throw new NotSupportedException($"{channels} channels are not supported");

        long maxFloats = maxSeconds > 0 ? (long)maxSeconds * sampleRate * channels : long.MaxValue;
        var chunks = new List<float[]>();
        int chunkFloats = 4096 * channels;
        long total = 0;

        while (total < maxFloats)
        {
            var buf = new float[chunkFloats];
            int read = mpeg.ReadSamples(buf, 0, chunkFloats);

            if (read <= 0)
                break;

            if (read < chunkFloats)
                Array.Resize(ref buf, read);

            chunks.Add(buf);

            total += read;
        }

        total -= total % channels;

        if (total == 0)
            throw new InvalidDataException("The MPEG audio file contains no decodable audio");

        var samples = new float[total];
        long pos = 0;

        foreach (var c in chunks)
        {
            int n = (int)Math.Min(c.Length, total - pos);

            Array.Copy(c, 0, samples, pos, n);

            pos += n;
        }

        return new PcmData { Channels = channels, SampleRate = sampleRate, Samples = samples };
    }
}