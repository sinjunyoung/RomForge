using Common.Models;

namespace Common.Services;

public interface IRomEstimator
{
    Task<EstimateResult> EstimateAsync(string inputPath, int compressionLevel, CancellationToken ct = default);
}