using Common;
using PBP.Core.Constants;
using PBP.Core.Models;

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

            var reporter = new ProgressReporter(gameTitle, gameId, totalEstimated: 0, progress);

            PsarPackager.WritePsar(outputStream, gameTitle, gameId, discInfos, psarOffset, compressionLevel, reporter.CreateAction(), ct);
            StartDatWriter.WriteStartDat(outputStream, basePbpBytes, assets.BootPng ?? EmbeddedAssetProvider.GetBlankImage());

            return outputPath;
        }, ct);
    }

    private static SfoFile BuildDefaultSfo(string gameId, string gameTitle)
    {
        var sfoBuilder = new SfoBuilder();

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
}