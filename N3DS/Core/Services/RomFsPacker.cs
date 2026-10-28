using Common;
using N3DS.Core.Interfaces;
using N3DS.Core.IO;
using N3DS.Core.Models;

namespace N3DS.Core.Services;

public static class RomFsPacker
{
    public static async Task PackAsync(Stream ncchStream, RomFsUnpackResult unpack, Stream output, long totalBytes = 0, IRomFsFileSource? patchSource = null, Action<long, long>? progress = null, Action<string, LogLevel>? log = null, CancellationToken ct = default)
    {
        long dataBase = unpack.DataLevel2Offset + unpack.RomFsHeader.DataOffset;

        Dictionary<string, long>? patchSizeMap = null;

        if (patchSource != null)
            patchSizeMap = await RomFsPatchSizeResolver.BuildPatchSizeMapAsync(unpack.Files, patchSource, ncchStream, dataBase, log, ct);

        var (totalSize, _, _, _, _) = RomFsLayoutCalculator.CalculateLayout(unpack.Directories, unpack.Files, patchSizeMap);
        long startPos = output.Position;

        if (output.CanSeek && output.Length < startPos + (long)totalSize)
            output.SetLength(startPos + (long)totalSize);

        await PackInternalAsync(ncchStream, dataBase, unpack.Directories, unpack.Files, output, startPos, totalBytes, patchSource, patchSizeMap, progress, log, ct);
    }

    private static async Task PackInternalAsync(Stream ncchStream, long dataBase, IReadOnlyList<RomFsDirNode> dirs, IReadOnlyList<RomFsFileNode> files, Stream output, long startPos, long totalBytes, IRomFsFileSource? patchSource = null, Dictionary<string, long>? patchSizeMap = null, Action<long, long>? progress = null, Action<string, LogLevel>? log = null, CancellationToken ct = default)
    {
        var layout = await RomFsMetadataBuilder.BuildAsync(output, startPos, dirs, files, patchSizeMap, ct);
        long level3AbsStart = startPos + (long)layout.OffLevel3;
        var hashingStream = new IvfcHashTreeWriter.Level3HashingStream(output, RomFsFormat.BlockSize);

        output.Position = level3AbsStart;

        await hashingStream.WriteAsync(layout.Meta, ct);

        ulong dataAreaPos = 0;
        long cumulativeWritten = 0;
        bool hasOriginalSource = ncchStream != null && ncchStream != Stream.Null;

        foreach (var file in files)
        {
            long actualSize = patchSizeMap?.TryGetValue(file.FullPath, out long ps) == true ? ps : (long)file.DataSize;

            if (actualSize == 0)
                continue;

            dataAreaPos = RomFsFormat.AlignUp(dataAreaPos, 0x10);

            long fileAbsPos = level3AbsStart + layout.RomFsHdrSize + (long)dataAreaPos;

            if (output.Position != fileAbsPos)
            {
                int padSize = (int)(fileAbsPos - output.Position);

                await hashingStream.WriteAsync(new byte[padSize], ct);
            }

            Func<CancellationToken, ValueTask<Stream?>>? getOriginal = hasOriginalSource ? (ct2 => RomFsPatchSizeResolver.ReadOriginalSliceAsync(ncchStream!, dataBase, file, ct2)) : null;

            Stream? patchStream = patchSource != null ? await patchSource.OpenFileAsync(file.FullPath, getOriginal, log, ct) : null;
            long before = cumulativeWritten;

            if (patchStream != null)
            {
                await using (patchStream)
                    await patchStream.CopyToAsync(hashingStream, patchStream.Length, totalBytes, progress != null ? (w, t) => progress(before + w, t) : null, ct);
            }
            else
            {
                ncchStream?.Position = dataBase + (long)file.DataOffset;

                await ncchStream!.CopyToAsync(hashingStream, (long)file.DataSize, totalBytes, progress != null ? (w, t) => progress(before + w, t) : null, ct);
            }

            dataAreaPos += (ulong)actualSize;
            cumulativeWritten += actualSize;
        }

        byte[] level3HashResult = hashingStream.GetHashResult();

        output.Position = startPos + (long)layout.OffLevel2Hash;

        await output.WriteAsync(level3HashResult, ct);

        await IvfcHashTreeWriter.GenHashLevelAsync(output, startPos + (long)layout.OffLevel2Hash, startPos + (long)layout.OffLevel1Hash, layout.Level2Size, RomFsFormat.BlockSize, ct);
        await IvfcHashTreeWriter.GenHashLevelAsync(output, startPos + (long)layout.OffLevel1Hash, startPos + (long)(layout.Off0 + (ulong)RomFsFormat.AlignUp(RomFsFormat.IvfcHeaderSize, RomFsFormat.IvfcHeaderAlign)), layout.Level1Size, RomFsFormat.BlockSize, ct);
    }
}