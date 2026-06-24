using Common;
using PBP.Core.Constants;
using PBP.Core.Models;
using System.Diagnostics;

namespace PBP.Core.Services;

public static class PbpPackager
{
    public static Task<string> WritePbpAsync(IReadOnlyList<DiscWriteInfo> discInfos, string gameId, string gameTitle, string outputPath, int compressionLevel, PbpAssets? assets, IProgress<ProgressInfo> progress, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            assets ??= new PbpAssets();

            var basePbpBytes = BaseResourceLoader.GetBasePbpBytes();

            PbpHeaderBuilder.EnsureRequiredAssets(assets, basePbpBytes);

            var sfo = BuildDefaultSfo(gameId, gameTitle);
            var header = PbpHeaderBuilder.BuildHeader(assets, sfo.Size);
            var psarOffset = header[9];
            using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 4 * 1024 * 1024, FileOptions.SequentialScan);

            WriteCommonSections(outputStream, header, sfo, assets, psarOffset);

            var reporter = CreateProgressReporter(gameTitle, gameId, progress, Stopwatch.GetTimestamp());

            PsarPackager.WritePsar(outputStream, gameTitle, gameId, discInfos, psarOffset, compressionLevel, ct, reporter);
            StartDatWriter.WriteStartDat(outputStream, basePbpBytes, assets.BootPng);

            return outputPath;
        }, ct);
    }

    private static SfoFile BuildDefaultSfo(string gameId, string gameTitle)
    {
        var sfoBuilder = new SFOBuilder();

        sfoBuilder.AddEntry(SfoKeys.BOOTABLE, 0x01);
        sfoBuilder.AddEntry(SfoKeys.CATEGORY, SfoValues.PS1Category);
        sfoBuilder.AddEntry(SfoKeys.DISC_ID, gameId);
        sfoBuilder.AddEntry(SfoKeys.DISC_VERSION, "1.00");
        sfoBuilder.AddEntry(SfoKeys.LICENSE, SfoValues.License);
        sfoBuilder.AddEntry(SfoKeys.PARENTAL_LEVEL, SfoValues.ParentalLevel);
        sfoBuilder.AddEntry(SfoKeys.PSP_SYSTEM_VER, SfoValues.PSPSystemVersion);
        sfoBuilder.AddEntry(SfoKeys.REGION, 0x8000);
        sfoBuilder.AddEntry(SfoKeys.TITLE, gameTitle);

        return sfoBuilder.Build();
    }

    private static void WriteCommonSections(Stream outputStream, uint[] header, SfoFile sfo, PbpAssets assets, uint psarOffset)
    {
        outputStream.Write(header, 0, 0x28);
        outputStream.WriteSFO(sfo);
        outputStream.WriteResource(assets.Icon0Png);
        outputStream.WriteResource(assets.Pic0Png);
        outputStream.WriteResource(assets.Pic1Png);
        outputStream.WriteResource(assets.DataPsp);

        var pos = (uint)outputStream.Position;

        for (var i = 0; i < psarOffset - pos; i++)
            outputStream.WriteByte(0);
    }

    private static Action<long, long> CreateProgressReporter(string gameTitle, string gameId, IProgress<ProgressInfo> progress, long startTime)
    {
        var reportLock = new object();
        var reportSw = Stopwatch.StartNew();
        var window = new Queue<(long ts, long written)>();
        double windowSec = 2.0;

        return (cur, total) =>
        {
            lock (reportLock)
            {
                if (cur < total && reportSw.ElapsedMilliseconds < 100)
                    return;

                long now = Stopwatch.GetTimestamp();

                window.Enqueue((now, cur));

                double freq = Stopwatch.Frequency;

                while (window.Count > 1 && (now - window.Peek().ts) / freq > windowSec)
                    window.Dequeue();

                double mibPerSec = 0;
                double etaSec = 0;

                if (window.Count >= 2)
                {
                    var (ts, written) = window.Peek();
                    double secSpan = (now - ts) / freq;
                    long bytesSpan = cur - written;
                    double avgSpeed = cur / ((now - startTime) / freq);
                    double windowSpeed = secSpan > 0 ? bytesSpan / secSpan : 0;
                    double progressRatio = total > 0 ? (double)cur / total : 0;
                    double blendedSpeed = avgSpeed * (1 - progressRatio) + windowSpeed * progressRatio;

                    mibPerSec = blendedSpeed / (1024.0 * 1024.0);
                    etaSec = blendedSpeed > 0 ? (total - cur) / blendedSpeed : 0;
                }

                double elapsedSec = (now - startTime) / freq;
                var elapsed = TimeSpan.FromSeconds(elapsedSec);
                var totalEta = TimeSpan.FromSeconds(elapsedSec + Math.Max(0, etaSec));
                int pct = total > 0 ? (int)(cur * 100 / total) : 0;

                if (pct > 100) 
                    pct = 100;

                var r = Utils.CalculateProgress(cur, total, gameTitle);

                progress?.Report(new ProgressInfo(pct, r.label, gameId, $"{mibPerSec:F1} MiB/s", $"{elapsed:mm\\:ss} / {totalEta:mm\\:ss}"));
                reportSw.Restart();
            }
        };
    }
}