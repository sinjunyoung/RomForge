using LibHac.Common.Keys;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.HacPack.Enums;
using NSW.HacPack.Services;
using System.IO;

namespace RomForge.Core.Services.Switch;

internal static class MergedProgramNcaBuilder
{
    public static Func<Stream> CreateRomfsOpener(Nca baseNca, Nca? updateNca)
    {
        EnsureSparseHasUpdate(baseNca, updateNca);

        int index = Nca.GetSectionIndexFromType(NcaSectionType.Data, baseNca.Header.ContentType);

        return () =>
        {
            var storage = updateNca != null ? baseNca.OpenStorageWithPatch(updateNca, index, IntegrityCheckLevel.None) : baseNca.OpenStorage(index, IntegrityCheckLevel.None);
            return storage.AsStream(FileAccess.Read);
        };
    }

    public static long GetRomfsSize(Nca baseNca, Nca? updateNca)
    {
        using var stream = CreateRomfsOpener(baseNca, updateNca)();
        return stream.Length;
    }

    public static (byte[] NcaId, byte[] Hash, long Size) BuildInto(Stream output, Nca baseNca, Nca? updateNca, ulong titleId, byte idOffset, KeySet keySet, int lz4Level, Action<long>? onAdvance, CancellationToken ct)
    {
        var headerSource = updateNca ?? baseNca;
        var exefsFiles = ReadCode(baseNca, updateNca, ct);
        var logoFiles = ReadLogo(baseNca, ct);
        byte keyGeneration = headerSource.Header.KeyGeneration == 0 ? (byte)1 : headerSource.Header.KeyGeneration;
        int keyGenerationIndex = keyGeneration - 1;

        if (keyGenerationIndex >= keySet.KeyAreaKeys.Length)
            throw new InvalidOperationException($"유효하지 않은 KeyGeneration입니다: {keyGeneration}");

        var settings = new NcaGenerationOptions
        {
            IdOffset = idOffset,
            SourceIdOffset = idOffset,
            TitleId = titleId,
            ExefsFiles = exefsFiles,
            LogoFiles = logoFiles,
            Plaintext = 0,
            HasTitleKey = 0,
            NcaSig = NcaSigType.Zero,
            TitleType = LibHac.Ncm.ContentMetaType.Application,
            SdkVersion = headerSource.Header.SdkVersion.Version,
            KeyGeneration = keyGeneration,
            KeySet = keySet,
            CompressRomfs = true,
            RomfsImageOpener = CreateRomfsOpener(baseNca, updateNca),
            KeyAreaKey = keySet.KeyAreaKeys[keyGenerationIndex][0].DataRo.ToArray()
        };

        if (exefsFiles.Count > 0)
            Lz4ProgramNcaBuilder.PatchNpdm(settings, exefsFiles);

        var (hash, size) = NcaGenerator.GenerateProgramNcaInto(output, settings, lz4Level, onAdvance, ct);
        return (hash[..0x10], hash, size);
    }

    private static List<(string Name, byte[] Data)> ReadCode(Nca baseNca, Nca? updateNca, CancellationToken ct)
    {
        if (updateNca != null && updateNca.CanOpenSection(NcaSectionType.Code))
        {
            using var patched = baseNca.OpenFileSystemWithPatch(updateNca, NcaSectionType.Code, IntegrityCheckLevel.None);
            return Lz4ProgramNcaBuilder.ReadFiles(patched, ct);
        }

        if (!baseNca.CanOpenSection(NcaSectionType.Code))
            return [];

        using var plain = baseNca.OpenFileSystem(NcaSectionType.Code, IntegrityCheckLevel.None);
        return Lz4ProgramNcaBuilder.ReadFiles(plain, ct);
    }

    private static List<(string Name, byte[] Data)>? ReadLogo(Nca baseNca, CancellationToken ct)
    {
        if (!baseNca.CanOpenSection(NcaSectionType.Logo))
            return null;

        using var fileSystem = baseNca.OpenFileSystem(NcaSectionType.Logo, IntegrityCheckLevel.None);
        return Lz4ProgramNcaBuilder.ReadFiles(fileSystem, ct);
    }

    private static void EnsureSparseHasUpdate(Nca baseNca, Nca? updateNca)
    {
        if (updateNca != null)
            return;

        for (int i = 0; i < 4; i++)
        {
            if (baseNca.SectionExists(i) && baseNca.GetFsHeader(i).ExistsSparseLayer())
                throw new InvalidOperationException("SparseStorage NCA는 업데이트가 필요합니다.");
        }
    }
}