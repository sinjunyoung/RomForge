using LibHac;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.HacPack.Enums;
using NSW.HacPack.Services;
using System.IO;
using Path = System.IO.Path;

namespace RomForge.Core.Services.Switch;

internal static class Lz4MetaNcaBuilder
{
    public static byte[] ReadCnmt(Nca metaNca)
    {
        using var fileSystem = metaNca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);

        foreach (var entry in fileSystem.EnumerateEntries("/", "*.cnmt"))
        {
            using var file = new UniqueRef<IFile>();

            fileSystem.OpenFile(ref file.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();
            file.Get.GetSize(out long size).ThrowIfFailure();

            var data = new byte[size];

            file.Get.Read(out long _, 0, data, ReadOption.None).ThrowIfFailure();

            return data;
        }

        throw new InvalidOperationException("Meta NCA에서 cnmt 파일을 찾을 수 없습니다.");
    }

    public static BuiltNca Build(Nca sourceMeta, byte[] cnmt, ulong titleId, KeySet keySet, string workDir, CancellationToken ct)
    {
        string metaDir = Path.Combine(workDir, "meta");
        string outDir = Path.Combine(metaDir, "nca");
        string tempDir = Path.Combine(metaDir, "temp");

        Directory.CreateDirectory(outDir);
        Directory.CreateDirectory(tempDir);

        string cnmtPath = Path.Combine(tempDir, "source.cnmt");

        File.WriteAllBytes(cnmtPath, cnmt);

        byte keyGeneration = sourceMeta.Header.KeyGeneration == 0 ? (byte)1 : sourceMeta.Header.KeyGeneration;
        int keyGenerationIndex = keyGeneration - 1;

        if (keyGenerationIndex >= keySet.KeyAreaKeys.Length)
            throw new InvalidOperationException($"유효하지 않은 KeyGeneration입니다: {keyGeneration}");

        var settings = new NcaGenerationOptions
        {
            IdOffset = 0,
            TitleId = titleId,
            TempDirectory = tempDir,
            OutDirectory = outDir,
            Plaintext = 0,
            HasTitleKey = 0,
            NcaSig = NcaSigType.Zero,
            TitleType = LibHac.Ncm.ContentMetaType.Application,
            SdkVersion = sourceMeta.Header.SdkVersion.Version,
            KeyGeneration = keyGeneration,
            KeySet = keySet,
            CnmtPath = cnmtPath,
            KeyAreaKey = keySet.KeyAreaKeys[keyGenerationIndex][0].DataRo.ToArray()
        };

        string path = NcaGenerator.GenerateMetaNca([settings], null, ct);

        return NcaFileInspector.Inspect(path);
    }
}