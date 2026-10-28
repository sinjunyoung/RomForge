using Common;
using LibHac.Common.Keys;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.Core.Models;
using NSW.Core.Services;
using NSW.HacPack.Enums;
using NSW.HacPack.Services;
using System.IO;
using Path = System.IO.Path;

namespace RomForge.Core.Services.Switch;

internal static class Lz4ProgramNcaBuilder
{
    public static BuiltNca Build(Nca source, ulong baseTitleId, byte idOffset, KeySet keySet, string workDir, int lz4Level, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        string programDir = Path.Combine(workDir, $"program_{idOffset}");
        string outDir = Path.Combine(programDir, "nca");
        string tempDir = Path.Combine(programDir, "temp");

        Directory.CreateDirectory(outDir);
        Directory.CreateDirectory(tempDir);

        var extractor = new NcaExtractor(keySet, _ => true);
        var context = new ProgressContext(1);
        string exefsDir = source.CanOpenSection(NcaSectionType.Code) ? extractor.ExtractExeFs(source, null, "exefs", programDir, context, ct) : string.Empty;
        string logoDir = extractor.ExtractLogo(source, "logo", programDir, context, ct) ?? string.Empty;
        byte keyGeneration = source.Header.KeyGeneration == 0 ? (byte)1 : source.Header.KeyGeneration;
        int keyGenerationIndex = keyGeneration - 1;

        if (keyGenerationIndex >= keySet.KeyAreaKeys.Length)
            throw new InvalidOperationException($"유효하지 않은 KeyGeneration입니다: {keyGeneration}");

        var settings = new NcaGenerationOptions
        {
            IdOffset = idOffset,
            SourceIdOffset = idOffset,
            TitleId = baseTitleId,
            TempDirectory = tempDir,
            OutDirectory = outDir,
            ExefsDirectory = exefsDir,
            LogoDirectory = logoDir,
            BackupDirectory = Path.Combine(programDir, "backups"),
            Plaintext = 0,
            HasTitleKey = 0,
            NcaSig = NcaSigType.Zero,
            TitleType = LibHac.Ncm.ContentMetaType.Application,
            SdkVersion = source.Header.SdkVersion.Version,
            KeyGeneration = keyGeneration,
            KeySet = keySet,
            CompressRomfs = true,
            RomfsImageOpener = () => source.OpenStorage(NcaSectionType.Data, IntegrityCheckLevel.None).AsStream(FileAccess.Read),
            KeyAreaKey = keySet.KeyAreaKeys[keyGenerationIndex][0].DataRo.ToArray()
        };

        if (!string.IsNullOrEmpty(exefsDir))
            NpdmProcessor.PatchNpdmMetadata(settings);

        string path = NcaGenerator.GenerateProgramNca(settings, lz4Level, progress, ct);

        return NcaFileInspector.Inspect(path);
    }
}