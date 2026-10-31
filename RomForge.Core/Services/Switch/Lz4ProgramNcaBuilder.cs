using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.HacPack.Enums;
using NSW.HacPack.Services;
using System.IO;

namespace RomForge.Core.Services.Switch;

internal static class Lz4ProgramNcaBuilder
{
    public static (byte[] NcaId, byte[] Hash, long Size) BuildInto(Stream output, Nca source, ulong baseTitleId, byte idOffset, KeySet keySet, int lz4Level, Action<long>? onAdvance, CancellationToken ct)
    {
        var exefsFiles = ReadSection(source, NcaSectionType.Code, ct) ?? [];
        var logoFiles = ReadSection(source, NcaSectionType.Logo, ct);
        byte keyGeneration = source.Header.KeyGeneration == 0 ? (byte)1 : source.Header.KeyGeneration;
        int keyGenerationIndex = keyGeneration - 1;

        if (keyGenerationIndex >= keySet.KeyAreaKeys.Length)
            throw new InvalidOperationException($"유효하지 않은 KeyGeneration입니다: {keyGeneration}");

        var settings = new NcaGenerationOptions
        {
            IdOffset = idOffset,
            SourceIdOffset = idOffset,
            TitleId = baseTitleId,
            ExefsFiles = exefsFiles,
            LogoFiles = logoFiles,
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

        if (exefsFiles.Count > 0)
            PatchNpdm(settings, exefsFiles);

        var (hash, size) = NcaGenerator.GenerateProgramNcaInto(output, settings, lz4Level, onAdvance, ct);

        return (hash[..0x10], hash, size);
    }

    private static List<(string Name, byte[] Data)>? ReadSection(Nca source, NcaSectionType sectionType, CancellationToken ct)
    {
        if (!source.CanOpenSection(sectionType))
            return null;

        using var fileSystem = source.OpenFileSystem(sectionType, IntegrityCheckLevel.None);

        return ReadFiles(fileSystem, ct);
    }

    internal static List<(string Name, byte[] Data)> ReadFiles(IFileSystem fileSystem, CancellationToken ct)
    {
        var files = new List<(string Name, byte[] Data)>();

        foreach (var entry in fileSystem.EnumerateEntries("/", "*", SearchOptions.RecurseSubdirectories))
        {
            ct.ThrowIfCancellationRequested();

            if (entry.Type == DirectoryEntryType.Directory || entry.FullPath.Count(c => c == '/') > 1)
                continue;

            using var file = new UniqueRef<IFile>();

            fileSystem.OpenFile(ref file.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();
            file.Get.GetSize(out long size).ThrowIfFailure();

            var data = new byte[size];

            file.Get.Read(out long _, 0, data, ReadOption.None).ThrowIfFailure();
            files.Add((entry.Name.ToString(), data));
        }

        return files;
    }

    internal static void PatchNpdm(NcaGenerationOptions settings, List<(string Name, byte[] Data)> exefsFiles)
    {
        int index = exefsFiles.FindIndex(f => f.Name == "main.npdm");

        if (index < 0)
            throw new FileNotFoundException("ExeFS에서 main.npdm을 찾을 수 없습니다.");

        using var stream = new MemoryStream(exefsFiles[index].Data);

        NpdmProcessor.PatchNpdmMetadata(settings, stream);
    }
}