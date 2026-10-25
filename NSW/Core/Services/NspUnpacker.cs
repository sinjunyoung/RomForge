using Common;
using LibHac.Common.Keys;
using NSW.Core.Models;

namespace NSW.Core.Services;

public class NspUnpacker(KeySet keySet) : NspUnpackerBase(keySet)
{
    public async Task<UnpackResult> Unpack(BuildRequest req, string outDir, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default) => await UnpackCore(req, outDir, true, progress, ct);

    protected override ProgressContext CreateProgressContext(List<string> inputPaths, CollectedNcas ncas)
    {
        long totalExtractSize = CalculateMatchedSize(ncas);
        return new ProgressContext(Math.Max(1L, totalExtractSize));
    }

    protected override bool ShouldExtract(string entryPath) => true;
}