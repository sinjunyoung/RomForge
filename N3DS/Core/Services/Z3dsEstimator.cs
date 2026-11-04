using System.Buffers;
using Common.Models;
using Common.Services;
using N3DS.Core.Crypto;
using N3DS.Core.IO;
using N3DS.Core.Models;
using ZstdSharp;
using ZstdSharp.Unsafe;

namespace N3DS.Core.Services;

public sealed class Z3dsEstimator : IRomEstimator
{
    private const int FrameSize = 32 * 1024 * 1024;
    private const int StrataCount = 4;
    private const int InitialSamplesPerStratum = 1;
    private const int MaxSamplesPerStratum = 2;
    private const double TargetRelativeMargin = 0.05;
    private static readonly ArrayPool<byte> SharedPool = ArrayPool<byte>.Shared;

    private sealed record FrameCandidate(long Index, int Stratum, int Order);
    private sealed record FrameSample(long Index, int Stratum, int OriginalSize, int CompressedSize)
    {
        public double Ratio => (double)CompressedSize / OriginalSize;
    }
    private sealed record SamplingPlan(int[] StratumSizes, List<FrameCandidate> Candidates);

    public async Task<EstimateResult> EstimateAsync(string inputPath, int compressionLevel, CancellationToken ct = default)
    {
        var (stream, totalSize, isSeekable) = await OpenInputStreamAsync(inputPath, ct);
        await using (stream)
        {
            if (totalSize <= 0) return new EstimateResult(totalSize, totalSize, 0);

            long fullFrames = totalSize / FrameSize;
            int tailSize = (int)(totalSize % FrameSize);
            var plan = CreateSamplingPlan(fullFrames, totalSize, inputPath);

            if (plan.Candidates.Count == 0)
            {
                return new EstimateResult(totalSize, totalSize, 0);
            }

            var initial = plan.Candidates.Where(x => x.Order < InitialSamplesPerStratum).OrderBy(x => x.Index).ToArray();
            var samples = (await CompressSamplesSeekAsync(stream, initial, fullFrames, tailSize, compressionLevel, ct)).ToList();

            if (samples.Count == 0)
            {
                long fallbackSize = new FileInfo(inputPath).Length;
                return new EstimateResult(fallbackSize, fallbackSize, fallbackSize);
            }

            var result = CalculateResult(totalSize, plan.StratumSizes, samples);

            if (MeetsTarget(result)) return result;

            var additional = plan.Candidates.Where(x => x.Order >= InitialSamplesPerStratum).OrderBy(x => x.Index).ToArray();
            if (additional.Length > 0)
            {
                var addSamples = await CompressSamplesSeekAsync(stream, additional, fullFrames, 0, compressionLevel, ct);
                if (addSamples.Length > 0)
                {
                    samples.AddRange(addSamples);
                    result = CalculateResult(totalSize, plan.StratumSizes, samples);
                    if (MeetsTarget(result)) return result;
                }
            }

            return result;
        }
    }

    private static SamplingPlan CreateSamplingPlan(long fullFrames, long totalSize, string inputPath)
    {
        if (fullFrames <= 0) return new SamplingPlan(Array.Empty<int>(), new());

        int strataCount = (int)Math.Min(StrataCount, fullFrames);
        int seed = unchecked((int)(totalSize ^ (totalSize >> 32)));
        foreach (char c in Path.GetFileName(inputPath)) seed = unchecked(seed * 31 + c);
        var random = new Random(seed);

        var strata = new List<long>[strataCount];
        var stratumSizes = new int[strataCount];

        for (int h = 0; h < strataCount; h++)
        {
            long start = (long)h * fullFrames / strataCount;
            long end = (long)(h + 1) * fullFrames / strataCount;
            var indices = new List<long>();
            for (long i = start; i < end; i++) indices.Add(i);

            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }
            strata[h] = indices;
            stratumSizes[h] = indices.Count;
        }

        var candidates = new List<FrameCandidate>();
        for (int order = 0; order < MaxSamplesPerStratum; order++)
        {
            for (int h = 0; h < strataCount; h++)
            {
                if (order >= strata[h].Count) continue;
                candidates.Add(new FrameCandidate(strata[h][order], h, order));
            }
        }

        return new SamplingPlan(stratumSizes, candidates);
    }

    private static async Task<FrameSample[]> CompressSamplesSeekAsync(Stream stream, IReadOnlyList<FrameCandidate> candidates, long fullFrames, int tailSize, int compressionLevel, CancellationToken ct)
    {
        var requests = new List<(long Index, int Stratum, int Size, bool IsTail)>();
        foreach (var c in candidates)
        {
            requests.Add((c.Index, c.Stratum, FrameSize, false));
        }
        if (tailSize > 0)
        {
            requests.Add((fullFrames, -1, tailSize, true));
        }

        if (requests.Count == 0) return Array.Empty<FrameSample>();

        var resultsList = new List<FrameSample>();
        var semaphore = new SemaphoreSlim(Environment.ProcessorCount);
        var tasks = new Task[requests.Count];

        for (int i = 0; i < requests.Count; i++)
        {
            int index = i;
            var req = requests[index];

            tasks[index] = Task.Run(async () =>
            {
                await semaphore.WaitAsync(ct);
                byte[] buffer = SharedPool.Rent(req.Size);
                try
                {
                    lock (stream)
                    {
                        stream.Position = req.Index * (long)FrameSize;
                        stream.ReadExactly(buffer, 0, req.Size);
                    }

                    // 패딩 블록(전부 0 또는 전부 0xFF)인지 검사하여 통계 왜곡 원천 차단
                    if (IsPaddingBlock(buffer, req.Size))
                    {
                        return; // 패딩 블록은 샘플에서 제외
                    }

                    int compressedSize;
                    using (var compressor = new Compressor(compressionLevel))
                    {
                        compressor.SetParameter(ZSTD_cParameter.ZSTD_c_windowLog, 25);
                        int maxBound = Compressor.GetCompressBound(req.Size);
                        byte[] compBuf = SharedPool.Rent(maxBound);
                        try
                        {
                            compressedSize = compressor.Wrap(buffer.AsSpan(0, req.Size), compBuf);
                            compressedSize = compressedSize > 0 ? compressedSize : req.Size;
                        }
                        finally
                        {
                            SharedPool.Return(compBuf);
                        }
                    }

                    lock (resultsList)
                    {
                        resultsList.Add(new FrameSample(req.Index, req.Stratum, req.Size, compressedSize));
                    }
                }
                finally
                {
                    SharedPool.Return(buffer);
                    semaphore.Release();
                }
            }, ct);
        }

        await Task.WhenAll(tasks);
        return resultsList.ToArray();
    }

    private static bool IsPaddingBlock(byte[] buffer, int size)
    {
        // 샘플 블록이 전부 0x00 또는 전부 0xFF로 채워진 패딩 영역인지 빠르게 검사
        byte first = buffer[0];
        if (first != 0x00 && first != 0xFF) return false;

        // 성능을 위해 간헐적으로 샘플링하여 검사 (전체를 다 돌면 느려지므로 주요 포인트만 확인)
        int step = Math.Max(1, size / 1024);
        for (int i = 0; i < size; i += step)
        {
            if (buffer[i] != first) return false;
        }
        return true;
    }

    private static EstimateResult CalculateResult(long totalSize, int[] stratumSizes, IReadOnlyCollection<FrameSample> samples)
    {
        if (samples.Count == 0) return new EstimateResult(totalSize, totalSize, totalSize);

        double estimatedSize = samples.Where(x => x.Stratum == -1).Sum(x => (double)x.CompressedSize);
        double variance = 0;
        int degreesOfFreedom = 0;

        for (int h = 0; h < stratumSizes.Length; h++)
        {
            int population = stratumSizes[h];
            var group = samples.Where(x => x.Stratum == h).ToArray();
            int count = group.Length;
            if (count == 0) continue;

            double mean = group.Average(x => x.Ratio);
            estimatedSize += mean * population * FrameSize;

            if (count >= population) continue;
            if (count < 2) continue;

            double squaredDifferences = 0;
            foreach (var sample in group)
            {
                double diff = sample.Ratio - mean;
                squaredDifferences += diff * diff;
            }

            double sampleVariance = squaredDifferences / (count - 1);
            double component = population * (double)population * (1.0 - (double)count / population) * sampleVariance / count;
            variance += component * FrameSize * FrameSize;
            degreesOfFreedom += count - 1;
        }

        double confidenceFactor = GetT95(degreesOfFreedom);
        double marginDouble = confidenceFactor * Math.Sqrt(Math.Max(0, variance));

        long outputBytes = estimatedSize >= long.MaxValue ? long.MaxValue : (long)Math.Round(estimatedSize, MidpointRounding.AwayFromZero);
        long marginBytes = marginDouble >= long.MaxValue ? long.MaxValue : (long)Math.Ceiling(marginDouble);

        return new EstimateResult(totalSize, outputBytes, marginBytes);
    }

    private static bool MeetsTarget(EstimateResult result) => result.OutputBytes > 0 && result.MarginBytes <= result.OutputBytes * TargetRelativeMargin;

    private static double GetT95(int df) => df switch
    {
        <= 0 => 1.96,
        1 => 12.706,
        2 => 4.303,
        3 => 3.182,
        4 => 2.776,
        5 => 2.571,
        6 => 2.447,
        7 => 2.365,
        8 => 2.306,
        9 => 2.262,
        10 => 2.228,
        11 => 2.201,
        12 => 2.179,
        13 => 2.160,
        14 => 2.145,
        15 => 2.131,
        16 => 2.120,
        17 => 2.110,
        18 => 2.101,
        19 => 2.093,
        20 => 2.086,
        _ => 1.96
    };

    private static async Task<(Stream Stream, long TotalSize, bool IsSeekable)> OpenInputStreamAsync(string inputPath, CancellationToken ct)
    {
        var fileStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.RandomAccess);
        try
        {
            byte[] headerBuffer = new byte[0x200];
            fileStream.Position = 0x4000;
            await fileStream.ReadExactlyAsync(headerBuffer.AsMemory(), ct);
            var header = NcchHeader.Parse(headerBuffer, 0);

            if (!header.NoCrypto)
            {
                var keyStore = KeyStoreProvider.Instance.KeyStore;
                var ncsdHeader = new SubStream(fileStream, 0, 0x4000);
                var decryptedStream = new NcchDecryptionStream(fileStream, 0x4000, keyStore);
                return (new ConcatStream(ncsdHeader, decryptedStream), fileStream.Length, false);
            }

            fileStream.Position = 0;
            return (fileStream, fileStream.Length, true);
        }
        catch
        {
            await fileStream.DisposeAsync();
            throw;
        }
    }
}