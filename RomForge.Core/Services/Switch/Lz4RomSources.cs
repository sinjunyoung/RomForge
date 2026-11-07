using LibHac.Common.Keys;
using LibHac.Ncm;
using LibHac.NSZ;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.Core;
using NSW.Core.Services;
using System.IO;

namespace RomForge.Core.Services.Switch;

public static class Lz4RomSources
{
    internal sealed record Candidate(Nca Nca, bool IsSparse, bool IsCompressed);

    public static bool HasSparseLayer(string path)
    {
        var keySet = KeySetProvider.Instance.KeySet;

        if (keySet == null)
            return false;

        var disposables = new List<IDisposable>();

        try
        {
            return Enumerate(keySet.Clone(), path, disposables).Any(c => c.IsSparse);
        }
        catch
        {
            return false;
        }
        finally
        {
            for (int i = disposables.Count - 1; i >= 0; i--)
                disposables[i]?.Dispose();
        }
    }

    internal static List<Candidate> Enumerate(KeySet keySet, string path, List<IDisposable> disposables)
    {
        var candidates = new List<Candidate>();
        var source = new SourceSet([path]);
        var sourceFs = source.Open(keySet, path, disposables);
        var entryPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in sourceFs.EnumerateEntries("/", "*"))
            entryPaths[NormalizeName(entry.Name.ToString())] = entry.FullPath;

        foreach (var meta in MetadataReader.GetMetadataFromContainer(keySet, path).Where(m => m.Type is ContentMetaType.Application or ContentMetaType.AddOnContent))
        {
            if (!entryPaths.TryGetValue(NormalizeName(meta.FileName), out var metaEntryPath) || ContainerEntryBuilder.OpenStorage(sourceFs, metaEntryPath, disposables) is not { } metaOpened)
                continue;

            var metaNca = OpenNca(keySet, metaOpened.Storage, metaEntryPath);
            byte[] cnmt = Lz4MetaNcaBuilder.ReadCnmt(metaNca);
            byte recordType = meta.Type == ContentMetaType.Application ? CnmtRecordPatcher.ProgramType : CnmtRecordPatcher.DataType;

            foreach (var record in CnmtRecordPatcher.ReadRecords(cnmt).Where(r => r.Type == recordType))
            {
                string entryName = $"{Convert.ToHexString(record.NcaId).ToLowerInvariant()}.nca";

                if (!entryPaths.TryGetValue(entryName, out var entryPath) || ContainerEntryBuilder.OpenStorage(sourceFs, entryPath, disposables) is not { } opened)
                    continue;

                var nca = OpenNca(keySet, opened.Storage, entryPath);
                bool sparse = nca.HasSparseLayer();

                if (!sparse && !nca.CanOpenSection(NcaSectionType.Data))
                    continue;

                candidates.Add(new Candidate(nca, sparse, !sparse && RomfsCompression.HasCompressedSection(nca)));
            }
        }

        return candidates;
    }

    private static string NormalizeName(string name) => name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase) ? Path.ChangeExtension(name, ".nca") : name;

    private static Nca OpenNca(KeySet keySet, LibHac.Fs.IStorage storage, string entryPath) => entryPath.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase) ? new Ncz(keySet, storage, NczReadMode.Fast) : new Nca(keySet, storage);
}