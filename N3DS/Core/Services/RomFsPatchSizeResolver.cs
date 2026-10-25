using Common;
using N3DS.Core.Interfaces;
using N3DS.Core.Models;

namespace N3DS.Core.Services;

public static class RomFsPatchSizeResolver
{
    public static async Task<Dictionary<string, long>> BuildPatchSizeMapAsync(IReadOnlyList<RomFsFileNode> files, IRomFsFileSource patchSource, Stream? ncchStream = null, long dataBase = 0, Action<string, LogLevel>? log = null, CancellationToken ct = default)
    {
        var map = new Dictionary<string, long>();

        bool hasOriginalSource = ncchStream != null && ncchStream != Stream.Null;

        foreach (var file in files)
        {
            Func<CancellationToken, ValueTask<Stream?>>? getOriginal = hasOriginalSource ? (ct2 => ReadOriginalSliceAsync(ncchStream!, dataBase, file, ct2)) : null;
            var stream = await patchSource.OpenFileAsync(file.FullPath, getOriginal, log, ct);

            if (stream != null)
            {
                await using (stream)
                    map[file.FullPath] = stream.Length;
            }
        }

        return map;
    }

    public static async ValueTask<Stream?> ReadOriginalSliceAsync(Stream ncchStream, long dataBase, RomFsFileNode file, CancellationToken ct)
    {
        if (file.DataSize == 0)
            return new MemoryStream([]);

        ncchStream.Position = dataBase + (long)file.DataOffset;

        byte[] buffer = new byte[(long)file.DataSize];

        await ncchStream.ReadExactlyAsync(buffer, ct);

        return new MemoryStream(buffer);
    }
}