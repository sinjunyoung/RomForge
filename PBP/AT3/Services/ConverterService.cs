using PBP.AT3.Enums;
using PBP.AT3.Models;

namespace PBP.AT3.Services
{
    public static class ConverterService
    {
        public static byte[] Convert(byte[] inputBytes, bool isWav, ConverterOptions? options = null)
        {
            if (inputBytes == null || inputBytes.Length == 0)
                throw new ArgumentException("입력 데이터가 비어있습니다.", nameof(inputBytes));

            options ??= new ConverterOptions();

            uint bitrate = options.BitrateKbps switch
            {
                Bitrate.Kbps66 => 66150u,
                Bitrate.Kbps105 => 104738u,
                Bitrate.Kbps132 => 132300u,
                _ => throw new ArgumentException("비트레이트는 66, 105, 132 중 하나여야 합니다.", nameof(options))
            };

            PcmData pcm = ReadInputFromMemory(inputBytes, isWav, options.MaxSeconds);

            if (options.MaxSeconds > 0 && pcm.SampleRate > 0)
            {
                long maxFrames = (long)options.MaxSeconds * pcm.SampleRate;

                if (pcm.FrameCount > maxFrames)
                    pcm.Samples = pcm.Samples[..(int)(maxFrames * pcm.Channels)];
            }

            var settings = new EncoderSettings
            {
                Bitrate = bitrate,
                NoTonalComponents = !options.EnableTonal,
                NoGainControl = !options.EnableGain
            };

            using var outputStream = new MemoryStream();

            FileEncoder.Encode(pcm, outputStream, settings, leaveOpen: true, progress: p =>
            {
                options.OnProgress?.Invoke(p);
            });

            return outputStream.ToArray();
        }

        private static PcmData ReadInputFromMemory(byte[] inputBytes, bool isWav, int maxSeconds)
        {
            using var ms = new MemoryStream(inputBytes);

            if (isWav)
                return WavReader.Read(ms);
            else
            {
                try
                {
                    return Mp3Reader.Read(ms, maxSeconds);
                }
                catch (InvalidDataException)
                {
                    throw new NotSupportedException("지원하지 않는 형식입니다.");
                }
            }
        }
    }
}