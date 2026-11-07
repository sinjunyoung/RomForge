using Common.Models;
using System.Buffers;
using System.Diagnostics;

namespace Common.Services;

public abstract class BaseRomEstimator
{
    private const double GoldenRatioConjugate = 0.6180339887498949;
    private const double Confidence95 = 1.96;
    private const int SmallestSample = 32;

    private readonly record struct FrameSample(long FrameIndex, int OriginalLen, int CompressedLen);

    protected virtual int MinSampleFrames => 4;

    protected virtual int MaxSampleFrames => 16;

    protected virtual double TimeBudgetSeconds => 0;

    protected virtual double TargetRelativeMargin => 0.02;

    protected virtual int BatchFrames => 128;

    protected abstract int FrameSize { get; }

    protected abstract Task<(Stream Stream, long TotalSize, bool IsSeekable)> OpenInputStreamAsync(string inputPath, CancellationToken ct);

    protected abstract int CompressBlock(byte[] buffer, int length, int level);

    public virtual async Task<EstimateResult> EstimateAsync(string inputPath, int compressionLevel, CancellationToken ct = default)
    {
        var (inputStream, totalSize, isSeekable) = await OpenInputStreamAsync(inputPath, ct);

        await using (inputStream)
        {
            if (totalSize <= 0)
                return new EstimateResult(totalSize, totalSize, 0);

            long totalFrames = (totalSize + FrameSize - 1) / FrameSize;

            if (totalFrames <= 0)
                return new EstimateResult(totalSize, totalSize, 0);

            long stride = (long)(totalFrames * GoldenRatioConjugate);

            while (GreatestCommonDivisor(stride, totalFrames) != 1)
                stride++;

            long position = totalSize.GetHashCode() & int.MaxValue;
            bool adaptive = isSeekable && TimeBudgetSeconds > 0;
            long cap = Math.Min(totalFrames, MaxSampleFrames);
            long minimum = adaptive ? Math.Min(MinSampleFrames, Math.Max(totalFrames / 16, SmallestSample)) : Math.Clamp(totalFrames / 16, MinSampleFrames, MaxSampleFrames);

            minimum = Math.Min(minimum, cap);

            double budget = TimeBudgetSeconds * Math.Min(1.0, totalSize / (double)(1L << 30));
            var clock = Stopwatch.StartNew();
            var samples = new List<FrameSample>();
            var result = new EstimateResult(totalSize, totalSize, 0);

            long[] TakeFrames(int count)
            {
                var chosen = new long[count];

                for (int i = 0; i < count; i++)
                {
                    chosen[i] = position % totalFrames;
                    position = chosen[i] + stride;
                }

                Array.Sort(chosen);

                return chosen;
            }

            do
            {
                ct.ThrowIfCancellationRequested();

                int wanted = adaptive ? (int)Math.Max(BatchFrames, minimum - samples.Count) : (int)minimum;
                int count = (int)Math.Min(wanted, cap - samples.Count);
                var frames = TakeFrames(count);

                if (isSeekable)
                    ReadAndCompress(inputStream, frames, totalSize, compressionLevel, samples, ct);
                else
                    await ReadSequentialAsync(inputStream, frames, totalSize, compressionLevel, samples, ct);

                result = Summarize(samples, totalSize, totalFrames);
            }
            while (adaptive && samples.Count < cap && (samples.Count < minimum || (clock.Elapsed.TotalSeconds < budget && result.MarginBytes > result.OutputBytes * TargetRelativeMargin)));

            return result;
        }
    }

    private void ReadAndCompress(Stream stream, long[] frames, long totalSize, int level, List<FrameSample> samples, CancellationToken ct)
    {
        var results = new FrameSample[frames.Length];
        object streamLock = new();

        Parallel.For(0, frames.Length, new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = ct
        },
        i =>
        {
            long frameIndex = frames[i];
            long offset = frameIndex * FrameSize;
            int length = (int)Math.Min(FrameSize, totalSize - offset);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(length);

            try
            {
                lock (streamLock)
                {
                    stream.Position = offset;
                    stream.ReadExactly(buffer, 0, length);
                }

                int compressed = CompressBlock(buffer, length, level);

                results[i] = new FrameSample(frameIndex, length, compressed > 0 ? compressed : length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        });

        samples.AddRange(results);
    }

    private async Task ReadSequentialAsync(Stream stream, long[] frames, long totalSize, int level, List<FrameSample> samples, CancellationToken ct)
    {
        byte[] buffer = new byte[FrameSize];
        int index = 0;
        long lastFrame = frames[^1];

        for (long i = 0; i <= lastFrame; i++)
        {
            ct.ThrowIfCancellationRequested();

            long offset = i * (long)FrameSize;
            int length = (int)Math.Min(FrameSize, totalSize - offset);

            await stream.ReadExactlyAsync(buffer.AsMemory(0, length), ct);

            if (frames[index] != i)
                continue;

            int compressed = CompressBlock(buffer, length, level);

            samples.Add(new FrameSample(i, length, compressed > 0 ? compressed : length));

            index++;
        }
    }

    private static EstimateResult Summarize(List<FrameSample> samples, long totalSize, long totalFrames)
    {
        double sampledBytes = samples.Sum(s => (double)s.OriginalLen);

        if (sampledBytes == 0)
            return new EstimateResult(totalSize, totalSize, 0);

        double ratio = samples.Sum(s => (double)s.CompressedLen) / sampledBytes;
        long estimatedOutputBytes = (long)(ratio * totalSize);
        var ordered = samples.OrderBy(s => s.FrameIndex).ToList();
        double squaredDifferences = 0;

        for (int i = 1; i < ordered.Count; i++)
        {
            double diff = (double)ordered[i].CompressedLen / ordered[i].OriginalLen - (double)ordered[i - 1].CompressedLen / ordered[i - 1].OriginalLen;

            squaredDifferences += diff * diff;
        }

        int count = ordered.Count;
        double variance = count > 1 ? (1.0 - (double)count / totalFrames) / count * squaredDifferences / (2.0 * (count - 1)) : 0;
        long margin = (long)(Confidence95 * Math.Sqrt(variance) * totalSize);

        return new EstimateResult(totalSize, estimatedOutputBytes, margin);
    }

    private static long GreatestCommonDivisor(long a, long b)
    {
        while (b != 0)
            (a, b) = (b, a % b);

        return a;
    }
}