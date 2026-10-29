using LibHac.Common.Keys;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.HacPack.Enums;
using NSW.HacPack.Services;
using System.IO;

namespace RomForge.Core.Services.Switch;

internal static class Lz4DataNcaBuilder
{
    public static (byte[] NcaId, byte[] Hash, long Size) BuildInto(Stream output, Nca source, ulong titleId, KeySet keySet, int lz4Level, Action<long>? onAdvance, CancellationToken ct)
    {
        byte keyGeneration = source.Header.KeyGeneration == 0 ? (byte)1 : source.Header.KeyGeneration;
        int keyGenerationIndex = keyGeneration - 1;

        if (keyGenerationIndex >= keySet.KeyAreaKeys.Length)
            throw new InvalidOperationException($"유효하지 않은 KeyGeneration입니다: {keyGeneration}");

        var settings = new NcaGenerationOptions
        {
            IdOffset = 0,
            TitleId = titleId,
            Plaintext = 0,
            HasTitleKey = 0,
            NcaSig = NcaSigType.Zero,
            TitleType = LibHac.Ncm.ContentMetaType.AddOnContent,
            NcaType = (LibHac.FsSystem.NcaHeader.ContentType)(byte)source.Header.ContentType,
            SdkVersion = source.Header.SdkVersion.Version,
            KeyGeneration = keyGeneration,
            KeySet = keySet,
            CompressRomfs = true,
            RomfsImageOpener = () => source.OpenStorage(NcaSectionType.Data, IntegrityCheckLevel.None).AsStream(FileAccess.Read),
            KeyAreaKey = keySet.KeyAreaKeys[keyGenerationIndex][0].DataRo.ToArray()
        };

        var (hash, size) = NcaGenerator.GenerateRomfsNcaInto(output, settings, "Data", lz4Level, onAdvance, ct);

        return (hash[..0x10], hash, size);
    }
}