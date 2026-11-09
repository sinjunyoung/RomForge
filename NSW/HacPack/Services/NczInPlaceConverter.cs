using Common.Services;
using LibHac.NSZ;
using LibHac.Tools.FsSystem;
using NSW.HacPack.IO;
using NSW.HacPack.Models;
using System.IO;

namespace NSW.HacPack.Services;

public static class NczInPlaceConverter
{
    public const long Gap = 0x2000000;

    private const long HeaderSize = 0x4000;

    public static long Convert(Stream file, long entryStart, long ncaStart, long ncaSize, NczOptions options)
    {
        var sync = new object();
        using var reader = new FileRegionReadStream(file, ncaStart, ncaSize, sync);
        using var storage = new StreamStorage(reader, true);
        var writer = new FileRegionWriteStream(file, entryStart, sync);
        var converter = new NcaToNczConverter(options.KeySet);
        var onRead = options.OnRead;
        ProgressReporter? reporter = null;

        if (onRead == null && options.Progress != null)
        {
            reporter = new ProgressReporter("NSZ 압축 중", string.Empty, Math.Max(1, ncaSize - HeaderSize), options.Progress);
            onRead = reporter.AddProgress;
        }

        converter.ConvertAsync(null!, storage, writer, options.CompressionMode, options.CompressionLevel, onRead, options.Cancellation).GetAwaiter().GetResult();
        reporter?.ForceReport();

        return writer.Length;
    }
}