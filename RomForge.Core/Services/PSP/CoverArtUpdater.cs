using PSP.PBP.Services;
using RomForge.Core.Models.PSP;

namespace RomForge.Core.Services.PSP;

public class CoverArtUpdater
{
    private CancellationTokenSource? _cts;

    public async Task<CoverArtResult?> FetchAsync(string gameId)
    {
        var old = Interlocked.Exchange(ref _cts, new CancellationTokenSource());

        if (old != null)
        {
            old.Cancel();
            old.Dispose();
        }

        var ct = _cts.Token;

        try
        {
            var meta = GameMetadataLookup.Find(gameId);

            var icon0Png = await CoverArtFetcher.TryDownloadIconPngAsync(gameId, ct);

            ct.ThrowIfCancellationRequested();

            var pic0Png = meta != null ? await GameMetadataLookup.TryDownloadImagePngAsync(meta.Pic0, ct) : null;

            ct.ThrowIfCancellationRequested();

            var pic1Png = meta != null ? await GameMetadataLookup.TryDownloadImagePngAsync(meta.Pic1, ct) : null;

            ct.ThrowIfCancellationRequested();

            return new CoverArtResult(icon0Png ?? EmbeddedAssetProvider.GetDefaultIcon0(), pic0Png ?? EmbeddedAssetProvider.GetDefaultPic0(), pic1Png ?? EmbeddedAssetProvider.GetDefaultPic1(), !string.IsNullOrWhiteSpace(meta?.ETitle) ? meta.ETitle : null);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}