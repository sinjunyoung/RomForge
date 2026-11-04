
using Common.Models;

namespace Common.Services;

public abstract class BaseRomEstimator
{
    private const double GoldenRatioConjugate = 0.6180339887498949;
    private const int MinSampleFrames = 4;
    private const int MaxSampleFrames = 16;

    protected abstract int FrameSize { get; }

    protected abstract Task<(Stream Stream, long TotalSize, bool IsSeekable)>
        OpenInputStreamAsync(string inputPath, CancellationToken ct);

    protected abstract int CompressBlock(byte[] buffer, int length, int level);

    public virtual async Task<EstimateResult> EstimateAsync(
        string inputPath,
        int compressionLevel,
        CancellationToken ct = default)
    {
        var (inputStream, totalSize, isSeekable) =
            await OpenInputStreamAsync(inputPath, ct);

        await using (inputStream)
        {
            if (totalSize <= 0)
                return new EstimateResult(totalSize, totalSize, 0);

            long totalFrames = (totalSize + FrameSize - 1) / FrameSize;

            if (totalFrames <= 0)
                return new EstimateResult(totalSize, totalSize, 0);

            int sampleCount = (int)Math.Min(
                totalFrames,
                Math.Clamp(totalFrames / 16, MinSampleFrames, MaxSampleFrames));

            long stride = (long)(totalFrames * GoldenRatioConjugate);

            while (GreatestCommonDivisor(stride, totalFrames) != 1)
                stride++;

            var chosenFrames = new long[sampleCount];

            long position = 0;

            for (int i = 0; i < sampleCount; i++)
            {
                chosenFrames[i] = position;
                position = (position + stride) % totalFrames;
            }

            Array.Sort(chosenFrames);

            var sampleResults =
                new List<(long FrameIndex, int OriginalLen, int CompressedLen)>(sampleCount);

            if (isSeekable)
            {
                object streamLock = new();

                Parallel.ForEach(
                    chosenFrames,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Environment.ProcessorCount,
                        CancellationToken = ct
                    },
                    frameIndex =>
                    {
                        long offset = frameIndex * (long)FrameSize;

                        int currentSize = (int)Math.Min(
                            FrameSize,
                            totalSize - offset);

                        byte[] buffer = new byte[currentSize];

                        lock (streamLock)
                        {
                            inputStream.Position = offset;
                            inputStream.ReadExactly(buffer, 0, currentSize);
                        }

                        int compSize = CompressBlock(
                            buffer,
                            currentSize,
                            compressionLevel);

                        int finalSize = compSize > 0
                            ? compSize
                            : currentSize;

                        lock (sampleResults)
                        {
                            sampleResults.Add((
                                frameIndex,
                                currentSize,
                                finalSize));
                        }
                    });
            }
            else
            {
                // 비탐색 스트림은 순차적으로 읽되 버퍼를 재사용한다.
                // 선택된 프레임만 실제 압축한다.
                byte[] buffer = new byte[FrameSize];

                int sampleIndex = 0;

                for (long i = 0; i < totalFrames; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    long offset = i * (long)FrameSize;

                    int currentSize = (int)Math.Min(
                        FrameSize,
                        totalSize - offset);

                    await inputStream.ReadExactlyAsync(
                        buffer.AsMemory(0, currentSize),
                        ct);

                    if (sampleIndex < sampleCount &&
                        chosenFrames[sampleIndex] == i)
                    {
                        int compSize = CompressBlock(
                            buffer,
                            currentSize,
                            compressionLevel);

                        int finalSize = compSize > 0
                            ? compSize
                            : currentSize;

                        sampleResults.Add((
                            i,
                            currentSize,
                            finalSize));

                        sampleIndex++;
                    }
                }
            }

            double sampledOriginalBytes =
                sampleResults.Sum(r => (double)r.OriginalLen);

            if (sampledOriginalBytes == 0)
                return new EstimateResult(totalSize, totalSize, 0);

            double ratio =
                sampleResults.Sum(r => (double)r.CompressedLen)
                / sampledOriginalBytes;

            long estimatedOutputBytes =
                (long)(ratio * totalSize);

            var ordered = sampleResults
                .OrderBy(r => r.FrameIndex)
                .ToList();

            double squaredDifferences = 0;

            for (int i = 1; i < ordered.Count; i++)
            {
                double diff =
                    (double)ordered[i].CompressedLen / ordered[i].OriginalLen
                    -
                    (double)ordered[i - 1].CompressedLen / ordered[i - 1].OriginalLen;

                squaredDifferences += diff * diff;
            }

            int count = ordered.Count;

            double variance = count > 1
                ? (1.0 - (double)count / totalFrames)
                  / count
                  * squaredDifferences
                  / (2.0 * (count - 1))
                : 0;

            long margin = (long)(
                1.96 * Math.Sqrt(variance) * totalSize);

            return new EstimateResult(
                totalSize,
                estimatedOutputBytes,
                margin);
        }
    }

    private static long GreatestCommonDivisor(long a, long b)
    {
        while (b != 0)
            (a, b) = (b, a % b);

        return a;
    }
}
