using Common.Services;
using Common.Localization;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.Core;
using NSW.HacPack.Services;
using PSP.PBP.Services;
using RomForge.Core.Models.Switch;
using System.IO;
using Path = System.IO.Path;

namespace RomForge.Core.Services.Switch;

public sealed class Lz4RomEstimator : BaseRomEstimator
{
    private readonly List<IDisposable> _disposables = [];

    private bool _alreadyCompressed;

    protected override int FrameSize => CompressedRomfsWriter.ChunkSize;

    protected override int MinSampleFrames => 128;

    protected override int MaxSampleFrames => 8192;

    protected override double TimeBudgetSeconds => 1;

    public async Task<Lz4FileEstimate> EstimateFileAsync(string path, int compressLevel, CancellationToken ct = default)
    {
        string extension = Path.GetExtension(path);

        if (extension.Equals(".nsz", StringComparison.OrdinalIgnoreCase) || extension.Equals(".xcz", StringComparison.OrdinalIgnoreCase) || KeySetProvider.Instance.KeySet == null)
            return Lz4FileEstimate.NotApplicable;

        try
        {
            var result = await EstimateAsync(path, compressLevel, ct);

            if (result.InputBytes <= 0)
                return _alreadyCompressed ? new Lz4FileEstimate(Lz4EstimateKind.AlreadyCompressed) : Lz4FileEstimate.NotApplicable;

            long fileBytes = new FileInfo(path).Length;
            long outputBytes = Math.Max(0, fileBytes - result.SavedBytes);

            return new Lz4FileEstimate(Lz4EstimateKind.Estimated, fileBytes, outputBytes, result.MarginBytes);
        }
        finally
        {
            for (int i = _disposables.Count - 1; i >= 0; i--)
                _disposables[i]?.Dispose();

            _disposables.Clear();
        }
    }

    protected override Task<(Stream Stream, long TotalSize, bool IsSeekable)> OpenInputStreamAsync(string inputPath, CancellationToken ct)
    {
        return Task.Run<(Stream Stream, long TotalSize, bool IsSeekable)>(() =>
        {
            var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Loc.S.ErrNoKeys);
            var streams = new List<Stream>();

            foreach (var candidate in Lz4RomSources.Enumerate(keySet.Clone(), inputPath, _disposables))
            {
                ct.ThrowIfCancellationRequested();

                if (candidate.IsSparse)
                    continue;

                if (candidate.IsCompressed)
                {
                    _alreadyCompressed = true;
                    continue;
                }

                streams.Add(candidate.Nca.OpenStorage(NcaSectionType.Data, IntegrityCheckLevel.None).AsStream(FileAccess.Read));
            }

            Stream stream = streams.Count switch
            {
                0 => Stream.Null,
                1 => streams[0],
                _ => new ConcatenatedStream(streams)
            };

            return (stream, stream.Length, true);
        });
    }

    protected override int CompressBlock(byte[] buffer, int length, int level) => Lz4BlockMeter.MeasureStoredSize(buffer, length, level);
}