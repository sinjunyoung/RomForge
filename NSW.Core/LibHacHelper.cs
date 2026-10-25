using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ns;
using LibHac.NSZ;
using LibHac.Tools.Es;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using LibHac.Tools.Ncm;
using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Text;
using static LibHac.Ns.ApplicationControlProperty;
using Path = System.IO.Path;
using Res = NSW.Core.Properties.Resources;

namespace NSW.Core;

public static class LibHacHelper
{
    public static string? FindBaseProgramDir(string unpackedDir)
    {
        if (!Directory.Exists(unpackedDir))
            return null;

        return Directory.GetDirectories(unpackedDir)
            .Select(d => new { Dir = d, Name = Path.GetFileName(d) })
            .Where(x => x.Name.Length == 16 && ulong.TryParse(x.Name, NumberStyles.HexNumber, null, out _))
            .Select(x => new { x.Dir, Id = ulong.Parse(x.Name, NumberStyles.HexNumber) })
            .OrderBy(x => x.Id)
            .Select(x => x.Dir)
            .FirstOrDefault();
    }

    public static IFileSystem OpenFileSystem(this LocalStorage storage, KeySet ks, string path)
    {
        IFileSystem fs;
        string ext = Path.GetExtension(path).ToLower();        

        if (ext == ".xci" || ext == ".xcz")
            fs = new Xci(ks, storage).OpenPartition(XciPartitionType.Secure);
        else if (ext == ".nsp" || ext == ".nsz")
        {
            var nsp = new PartitionFileSystem();
            nsp.Initialize(storage).ThrowIfFailure();
            fs = nsp;
        }
        else
            throw new NotSupportedException("지원하지 않는 ROM 형식입니다.");

        return fs;
    }

    public static void RegisterTickets(this KeySet ks, IFileSystem fs)
    {
        foreach (var entry in fs.EnumerateEntries("/", "*.tik"))
        {
            using var tikFile = new UniqueRef<IFile>();

            if (fs.OpenFile(ref tikFile.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).IsSuccess())
            {
                var ticket = new Ticket(tikFile.Get.AsStream());
                var rightsId = new RightsId(ticket.RightsId);

                if (!ks.ExternalKeySet.Contains(rightsId))
                    ks.ExternalKeySet.Add(rightsId, new LibHac.Spl.AccessKey(ticket.GetTitleKey(ks)));
            }
        }
    }

    public static Stream GetDecodedStream(IFile file, string fileName, KeySet keySet)
    {
        var stream = file.AsStream();

        if (Path.GetExtension(fileName).Equals(".ncz", StringComparison.CurrentCultureIgnoreCase))
        {
            var ncz = new Ncz(keySet, stream, NczReadMode.Original);

            return ncz.BaseStorage.AsStream();
        }

        return stream;
    }

    public static Cnmt? GetCnmtFromNca(this IFile ncaFile, KeySet ks)
    {
        try
        {
            var nca = new Nca(ks, ncaFile.AsStorage());

            if (nca.Header.ContentType != NcaContentType.Meta) 
                return null;

            using var metaFs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);
            var cnmtEntry = metaFs.EnumerateEntries("/", "*.cnmt").FirstOrDefault();

            if (cnmtEntry == null) 
                return null;

            using var cnmtFile = new UniqueRef<IFile>();

            if (metaFs.OpenFile(ref cnmtFile.Ref, cnmtEntry.FullPath.ToU8Span(), OpenMode.Read).IsFailure())
                return null;

            return new Cnmt(cnmtFile.Get.AsStream());
        }
        catch 
        { 
            return null;
        }
    }

    public static Cnmt? GetCnmtFromNca(this IStorage ncaStorage, KeySet ks)
    {
        try
        {
            var nca = new Nca(ks, ncaStorage);

            if (nca.Header.ContentType != NcaContentType.Meta) 
                return null;

            using var metaFs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);
            var cnmtEntry = metaFs.EnumerateEntries("/", "*.cnmt").FirstOrDefault();

            if (cnmtEntry == null) 
                return null;

            using var cnmtFile = new UniqueRef<IFile>();

            if (metaFs.OpenFile(ref cnmtFile.Ref, cnmtEntry.FullPath.ToU8Span(), OpenMode.Read).IsFailure())
                return null;

            return new Cnmt(cnmtFile.Get.AsStream());
        }
        catch 
        { 
            return null;
        }
    }

    public static ApplicationControlProperty? GetControlProperty(this Nca nca)
    {
        try
        {
            using var ctrlFs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);

            if (!ctrlFs.FileExists("/control.nacp"))
                return null;

            using var nFile = new UniqueRef<IFile>();

            ctrlFs.OpenFile(ref nFile.Ref, "/control.nacp".ToU8Span(), OpenMode.Read).ThrowIfFailure();

            var control = new ApplicationControlProperty();

            nFile.Get.Read(out _, 0, SpanHelpers.AsByteSpan(ref control)).ThrowIfFailure();

            return control;
        }
        catch
        { 
            return null;
        }
    }

    public static ApplicationControlProperty? GetControlProperty(this IStorage ncaStorage, KeySet keySet)
    {
        try
        {
            var nca = new Nca(keySet, ncaStorage);
            using var ctrlFs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);

            if (!ctrlFs.FileExists("/control.nacp"))
                return null;

            using var nFile = new UniqueRef<IFile>();

            ctrlFs.OpenFile(ref nFile.Ref, "/control.nacp".ToU8Span(), OpenMode.Read).ThrowIfFailure();

            var control = new ApplicationControlProperty();

            nFile.Get.Read(out _, 0, SpanHelpers.AsByteSpan(ref control)).ThrowIfFailure();

            return control;
        }
        catch 
        { 
            return null;
        }
    }

    public static byte[]? GetIconData(this IFileSystem cFs, Language lang)
    {
        string iconPath = $"/icon_{lang}.dat";

        if (!cFs.FileExists(iconPath)) 
            return null;

        using var logoFile = new UniqueRef<IFile>();

        if (cFs.OpenFile(ref logoFile.Ref, iconPath.ToU8Span(), OpenMode.Read).IsFailure()) 
            return null;

        logoFile.Get.GetSize(out long size).ThrowIfFailure();

        byte[] iconBuf = new byte[size];

        logoFile.Get.Read(out _, 0, iconBuf).ThrowIfFailure();

        return iconBuf;
    }

    public static string GetTitleIdFromRom(string path)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);
        using var storage = new LocalStorage(path, FileAccess.Read);
        IFileSystem fs = storage.OpenFileSystem(keySet, path);
        var entries = fs.EnumerateEntries("/", "*.nca")
            .Concat(fs.EnumerateEntries("/", "*.ncz"))
            .Where(e => e.Type == DirectoryEntryType.File);

        foreach (var entry in entries)
        {
            using var ncaFile = new UniqueRef<IFile>();

            if (fs.OpenFile(ref ncaFile.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).IsFailure())
                continue;

            try
            {
                bool isNcz = entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase);
                Nca nca = isNcz ? new Ncz(keySet, ncaFile.Release().AsStream(), NczReadMode.Fast) : new Nca(keySet, ncaFile.Release().AsStorage());

                if (nca.Header.ContentType == NcaContentType.Program)
                    return nca.Header.TitleId.ToString("X16");
            }
            catch 
            { 
                continue; 
            }
        }

        throw new Exception("Title ID를 찾을 수 없습니다.");
    }

    public static (byte KeyGeneration, uint SdkVersion) ReadControlNcaInfo(string ncaPath)
    {
        if (!File.Exists(ncaPath))
            throw new FileNotFoundException("control.nca 파일이 없습니다.", ncaPath);

        using var controlFs = new FileStream(ncaPath, FileMode.Open, FileAccess.Read);
        var storage = new StreamStorage(controlFs, false);
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);
        var nca = new Nca(keySet, storage);

        return (nca.Header.KeyGeneration, nca.Header.SdkVersion.Version);
    }

    public static (string KrTitle, string EnTitle, string DisplayVersion, ulong TitleId) ReadNacpInfo(string nacpPath)
    {
        if (!File.Exists(nacpPath))
            throw new FileNotFoundException("control.nacp 파일이 없습니다.", nacpPath);

        byte[] nacpData = File.ReadAllBytes(nacpPath);
        var control = new ApplicationControlProperty();

        nacpData.AsSpan().CopyTo(SpanHelpers.AsByteSpan(ref control));

        var titles = control.Title;
        int langCount = control.TitleCompression == TitleCompressionValue.Enable ? Constants.ExtendedLanguageCount : Constants.LegacyLanguageCount;
        ulong titleId = BinaryPrimitives.ReadUInt64LittleEndian(nacpData.AsSpan(0x30B0));
        string displayVersion = Encoding.UTF8.GetString(nacpData, 0x3060, 0x10).TrimEnd('\0');
        string enTitle = titles[(int)Language.AmericanEnglish].NameString.ToString().Trim('\0', ' ');
        string krTitle = titles[(int)Language.Korean].NameString.ToString().Trim('\0', ' ');

        if (string.IsNullOrWhiteSpace(krTitle) && string.IsNullOrWhiteSpace(enTitle))
        {
            for (int i = 0; i < langCount; i++)
            {
                string fallback = titles[i].NameString.ToString().Trim('\0', ' ');

                if (!string.IsNullOrWhiteSpace(fallback))
                {
                    krTitle = enTitle = fallback;
                    break;
                }
            }
        }
        else if (string.IsNullOrWhiteSpace(krTitle))
            krTitle = enTitle;
        else if (string.IsNullOrWhiteSpace(enTitle))
            enTitle = krTitle;

        return (krTitle, enTitle, displayVersion, titleId);
    }

    public static string ExtractNcaId(string fileName)
    {
        string stem = Path.GetFileNameWithoutExtension(fileName);

        if (stem.EndsWith(".cnmt", StringComparison.OrdinalIgnoreCase))
            stem = Path.GetFileNameWithoutExtension(stem);

        return stem.Length >= 32 ? stem[..32].ToLowerInvariant() : string.Empty;
    }

    public static string GetBaseTitleId(string titleId)
    {
        if (!ulong.TryParse(titleId, NumberStyles.HexNumber, null, out ulong tid))
            return string.Empty;

        return (tid & 0xFFFFFFFFFFFF0000UL).ToString("X16");
    }

    public static bool HasSparseLayer(this Nca nca)
    {
        return Enumerable.Range(0, 4)
            .Any(i => nca.SectionExists(i) && nca.GetFsHeader(i).ExistsSparseLayer());
    }
}