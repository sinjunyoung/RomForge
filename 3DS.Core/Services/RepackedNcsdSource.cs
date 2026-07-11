using _3DS.Core.Interfaces;
using _3DS.Core.Models;
using Common;

namespace _3DS.Core.Services;

public class RepackedNcsdSource : INcsdSource
{
    private readonly Dictionary<int, (NcchUnpackResult unpack, byte[] exefsBlock, Stream ncchSource, RomFsUnpackResult? romFs, IRomFsFileSource? patchSource, long ncchSize)> _ncchs;

    private RepackedNcsdSource(Dictionary<int, (NcchUnpackResult unpack, byte[] exefsBlock, Stream ncchSource, RomFsUnpackResult? romFs, IRomFsFileSource? patchSource, long ncchSize)> ncchs, IReadOnlyList<Contents> contents)
    {
        _ncchs = ncchs;
        Contents = contents;
    }

    public static async Task<RepackedNcsdSource> CreateAsync(Dictionary<int, (NcchUnpackResult unpack, byte[] exefsBlock, Stream ncchSource, RomFsUnpackResult? romFs, IRomFsFileSource? patchSource)> ncchs, IReadOnlyList<Contents> contents, CancellationToken ct = default)
    {
        var resolved = new Dictionary<int, (NcchUnpackResult, byte[], Stream, RomFsUnpackResult?, IRomFsFileSource?, long)>();
        var updatedContents = new List<Contents>();

        foreach (var chunk in contents)
        {
            if (ncchs.TryGetValue(chunk.ContentIndex, out var entry))
            {
                var (unpack, exefsBlock, ncchSource, romFs, patchSource) = entry;
                long size = await NcchBuilder.CalculateSizeAsync(unpack, exefsBlock, romFs, patchSource, ncchSource, ct);

                resolved[chunk.ContentIndex] = (unpack, exefsBlock, ncchSource, romFs, patchSource, size);
                updatedContents.Add(new Contents
                {
                    ContentId = chunk.ContentId,
                    ContentIndex = chunk.ContentIndex,
                    ContentSize = size,
                    ContentType = chunk.ContentType,
                });
            }
            else
            {
                updatedContents.Add(chunk);
            }
        }

        return new RepackedNcsdSource(resolved, updatedContents);
    }

    public IReadOnlyList<Contents> Contents { get; }

    public Action<string, LogLevel, string>? Log { get; init; }

    public async ValueTask<(Stream ncchStream, long ncchSize)> OpenContentDecrypted(int contentIndex)
    {
        var (unpack, exefsBlock, ncchSource, romFs, patchSource, ncchSize) = _ncchs[contentIndex];
        var output = new MemoryStream();

        await NcchBuilder.BuildAsync(unpack, exefsBlock, ncchSource, romFs, output);

        output.Position = 0;

        return (output, ncchSize);
    }

    public async Task WriteContentAsync(int contentIndex, Stream output, long totalBytes, Action<long, long>? progress, CancellationToken ct)
    {
        var (unpack, exefsBlock, ncchSource, romFs, patchSource, ncchSize) = _ncchs[contentIndex];
        long? basePos = output.CanSeek ? output.Position : null;

        await NcchBuilder.BuildAsync(unpack, exefsBlock, ncchSource, romFs, output, patchSource, progress, ct);

        if (basePos.HasValue)
            output.Position = basePos.Value + ncchSize;
    }

    public ValueTask<NcchHeader> GetNcchHeaderAsync(int contentIndex, CancellationToken ct = default)
    {
        var (unpack, _, _, _, _, _) = _ncchs[contentIndex];

        return ValueTask.FromResult(unpack.Header);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}