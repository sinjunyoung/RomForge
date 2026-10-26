using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.NSZ;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.Core;
using System.IO;
using Path = System.IO.Path;

namespace RomForge.Core.Services.Switch;

internal static class ContainerEntryBuilder
{
    public static (string FinalName, Func<Stream, Action<long>, Task> Writer, long Size, string Label)? BuildNcaEntry(string entryName, string originalExt, IStorage currentStorage, long size, Nca nca, 
        string label, bool useCompression, bool useBlockMode, int compressionLevel, bool forceKeyGen0, KeySet keySet, Dictionary<string, NcaToNczConverter> converters, string? copyStatus = null, CancellationToken ct = default)
    {
        string copyLabel = copyStatus == null ? label : $"{label} [{copyStatus}]";
        int keyGen = forceKeyGen0 ? 0 : (int)nca.Header.KeyGeneration;

        if (originalExt == ".ncz")
        {
            var ncz = new Ncz(keySet, currentStorage, NczReadMode.Original);
            var decStorage = ncz.BaseStorage;

            decStorage.GetSize(out long decSize).ThrowIfFailure();

            if (useCompression && nca.Header.ContentType is NcaContentType.Program or NcaContentType.PublicData)
            {
                string finalName = nca.HasSparseLayer() ? entryName : Path.ChangeExtension(entryName, ".ncz");
                var converter = new NcaToNczConverter(keySet);

                converters[Path.ChangeExtension(entryName, ".nca")] = converter;

                var cap = decStorage;

                return (finalName, async (s, onRead) =>
                {
                    var header = await NcaRecryptService.GetRecryptedHeaderAsync(cap, keyGen, keySet, ct);
                    using var hs = new MemoryStream(header);

                    await converter.ConvertAsync(hs, cap, s, useBlockMode, compressionLevel, onRead, ct);
                }, decSize, label);
            }
            else
            {
                string finalName = Path.ChangeExtension(entryName, ".nca");
                var cap = decStorage;

                return (finalName, async (s, onRead) =>
                {
                    await NcaRecryptService.RecryptAsync(cap.AsStream(), s, keyGen, keySet, onRead, ct);
                }, decSize, copyLabel);
            }
        }

        if (useCompression && nca.Header.ContentType is NcaContentType.Program or NcaContentType.PublicData)
        {
            string finalName = nca.HasSparseLayer() ? entryName : Path.ChangeExtension(entryName, ".ncz");
            var converter = new NcaToNczConverter(keySet);

            converters[Path.ChangeExtension(entryName, ".nca")] = converter;

            var cap = currentStorage;

            return (finalName, async (s, onRead) =>
            {
                var header = await NcaRecryptService.GetRecryptedHeaderAsync(cap, keyGen, keySet, ct);
                using var hs = new MemoryStream(header);

                await converter.ConvertAsync(hs, cap, s, useBlockMode, compressionLevel, onRead, ct);
            }, size, label);
        }
        else
        {
            var cap = currentStorage;

            return (entryName, async (s, onRead) =>
            {
                await NcaRecryptService.RecryptAsync(cap.AsStream(), s, keyGen, keySet, onRead, ct);
            }, size, copyLabel);
        }
    }

    public static (IStorage Storage, long Size)? OpenStorage(IFileSystem fileSystem, string fullPath, List<IDisposable> disposables, bool skipEmpty = false)
    {
        var fileRef = new UniqueRef<IFile>();

        if (!fileSystem.OpenFile(ref fileRef.Ref, fullPath.ToU8Span(), OpenMode.Read).IsSuccess())
            return null;

        fileRef.Get.GetSize(out long size).ThrowIfFailure();

        if (skipEmpty && size == 0)
        {
            fileRef.Destroy();
            return null;
        }

        IFile rawFile = fileRef.Release();

        disposables.Add(rawFile);

        IStorage storage = new FileStorage(rawFile);

        disposables.Add(storage);

        return (storage, size);
    }

    public static (string Name, Func<Stream, Action<long>, Task> Writer, long EstimatedSize, string Label) CreateCopyEntry(string name, IStorage storage, long size, CancellationToken ct) => (name, async (s, onRead) => await Common.Utils.CopyStreamAsync(storage.AsStream(), s, onRead, ct), size, name);
}