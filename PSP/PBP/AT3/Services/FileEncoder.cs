using PSP.PBP.AT3.Enums;
using PSP.PBP.AT3.Models;

namespace PSP.PBP.AT3.Services;

public static class FileEncoder
{
    public static void Encode(PcmData pcm, Stream output, EncoderSettings settings, bool leaveOpen = false, Action<double>? progress = null)
    {
        if (pcm.Channels != 1 && pcm.Channels != 2)
            throw new NotSupportedException("Only mono and stereo input is supported");

        if (pcm.FrameCount == 0)
            throw new InvalidDataException("There is no audio data to encode");

        pcm = Resampler.ToRate(pcm, CodecInfo.SampleRateHz);

        long minFrames = CodecInfo.SampleRateHz * 3 / 2;

        if (pcm.FrameCount < minFrames)
        {
            var padded = new float[minFrames * pcm.Channels];

            Array.Copy(pcm.Samples, padded, pcm.Samples.Length);

            pcm.Samples = padded;
        }

        var containerParams = CodecInfo.GetContainerParamsForBitrate(settings.Bitrate);
        long totalSamples = pcm.FrameCount;
        using var writer = new Writer(output, 2, containerParams.FrameSize, containerParams.JointStereo, totalSamples, leaveOpen);
        var encoder = new Encoder(writer, containerParams, pcm.Channels, settings);
        int frameLen = CodecInfo.NumSamples * pcm.Channels;
        var frame = new float[frameLen];
        long processed = 0;
        long pos = 0;

        while (processed < totalSamples)
        {
            Array.Clear(frame);

            long available = Math.Min(frameLen, pcm.Samples.Length - pos);

            if (available > 0)
                Array.Copy(pcm.Samples, pos, frame, 0, available);

            pos += frameLen;

            if (encoder.ProcessFrame(frame) == ProcessResult.Processed)
            {
                processed += CodecInfo.NumSamples;

                progress?.Invoke(Math.Min(1.0, (double)processed / totalSamples));
            }
        }
    }
}