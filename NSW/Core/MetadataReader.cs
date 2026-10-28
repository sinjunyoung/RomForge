using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Ns;
using LibHac.NSZ;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using LibHac.Tools.Ncm;
using NSW.Core.Models;
using NSW.Core.Services;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using static LibHac.Ns.ApplicationControlProperty;

namespace NSW.Core;

public static class MetadataReader
{
    private static readonly string[] sourceArray = ["B", "U", "D"];

    public static Language Current
    {
        get
        {
            var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

            return lang switch
            {
                "en" => Language.AmericanEnglish,
                "de" => Language.German,
                "es" => Language.Spanish,
                "fr" => Language.French,
                "ja" => Language.Japanese,
                "ko" => Language.Korean,
                "ru" => Language.Russian,
                "zh" => GetChineseVariant,
                _ => Language.AmericanEnglish
            };
        }
    }

    private static Language GetChineseVariant
    {
        get
        {
            var culture = CultureInfo.CurrentUICulture.Name;

            if (culture.StartsWith("zh-TW") || culture.StartsWith("zh-HK"))
                return Language.TraditionalChinese;

            return Language.SimplifiedChinese;
        }
    }

    public static string DetectFileType(KeySet keySet, string path)
    {
        if (keySet == null)
            return "Not Key";

        try
        {
            using var storage = new LocalStorage(path, FileAccess.Read);
            using var fs = storage.OpenFileSystem(keySet, path);
            var foundTypes = new HashSet<string>();
            var entries = fs.EnumerateEntries("/", "*.nca")
                .Concat(fs.EnumerateEntries("/", "*.ncz"))
                .Where(e => e.Type == DirectoryEntryType.File);

            foreach (var entry in entries)
            {
                using var file = new UniqueRef<IFile>();

                if (fs.OpenFile(ref file.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).IsFailure())
                    continue;

                var cnmt = file.Get.GetCnmtFromNca(keySet);

                if (cnmt != null)
                {
                    var letter = GetContentMetaTypeTag(cnmt.Type)[..1];

                    if (letter != "?")
                        foundTypes.Add(letter);
                }
            }

            return foundTypes.Count == 0 ? "?" : string.Join("+", sourceArray.Where(foundTypes.Contains));
        }
        catch 
        { 
            return "?"; 
        }
    }

    public static GameFileInfo GetGameFileInfo(KeySet keySet, string path)
    {
        var info = new GameFileInfo { Path = path };

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return info;

        try
        {
            using var storage = new LocalStorage(path, FileAccess.Read);
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            using IFileSystem fs = storage.OpenFileSystem(keySet, path);
            var types = new HashSet<string>();
            var entries = fs.EnumerateEntries("/", "*.nca").ToList();
            string? baseTitleId = null;
            string? anyTitleId = null;

            foreach (var entry in entries)
            {
                using var file = new UniqueRef<IFile>();

                if (fs.OpenFile(ref file.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).IsFailure())
                    continue;

                var nca = new Nca(keySet, file.Release().AsStorage());

                if (nca.Header.ContentType == NcaContentType.Meta)
                {
                    using var ncaFs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);

                    foreach (var cnmtEntry in ncaFs.EnumerateEntries("/", "*.cnmt"))
                    {
                        using var cnmtFile = new UniqueRef<IFile>();

                        if (ncaFs.OpenFile(ref cnmtFile.Ref, cnmtEntry.FullPath.ToU8Span(), OpenMode.Read).IsSuccess())
                        {
                            var cnmt = new Cnmt(cnmtFile.Get.AsStream());
                            string thisTitleId = cnmt.TitleId.ToString("X16");

                            anyTitleId ??= thisTitleId;

                            if (cnmt.Type == ContentMetaType.Application)
                                baseTitleId = thisTitleId;

                            string? t = GetContentMetaTypeTag(cnmt.Type)[..1];

                            if (t != null)
                                types.Add(t);
                        }
                    }
                }
                else if (nca.Header.ContentType == NcaContentType.Control)
                {
                    using var cFs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);
                    using var nacpFile = new UniqueRef<IFile>();

                    if (cFs.OpenFile(ref nacpFile.Ref, "/control.nacp".ToU8Span(), OpenMode.Read).IsSuccess())
                    {
                        var control = new ApplicationControlProperty();

                        nacpFile.Get.Read(out _, 0, SpanHelpers.AsByteSpan(ref control)).ThrowIfFailure();

                        info.DisplayVersion = control.DisplayVersionString.ToString().Trim('\0').Trim();

                        var (titleName, developer, language) = control.GetTitleByLanguage(Current);

                        info.TitleName = titleName;
                        info.Developer = developer;
                        info.IconData = cFs.GetIconData(language);
                    }
                }
            }

            if (baseTitleId != null || anyTitleId != null)
                info.TitleId = baseTitleId ?? anyTitleId!;

            info.Type = types.Count == 0 ? "?" : string.Join("+", sourceArray.Where(types.Contains));
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
        return info;
    }

    public static List<MetadataResult> GetMetadataFromContainer(KeySet keySet, string path, bool readIcon = false)
    {
        var results = new List<MetadataResult>();

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return results;

        using var storage = new LocalStorage(path, FileAccess.Read);
        IFileSystem fs = storage.OpenFileSystem(keySet, path);
        var allFiles = new Dictionary<string, IStorage>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in fs.EnumerateEntries("/", "*"))
        {
            var file = new UniqueRef<IFile>();

            if (fs.OpenFile(ref file.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).IsSuccess())
                allFiles[entry.Name] = file.Release().AsStream().AsStorage();
        }

        foreach (var cnmtNcaName in allFiles.Keys.Where(k => k.EndsWith(".cnmt.nca", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                using var ncaFile = allFiles[cnmtNcaName].AsFile(OpenMode.Read);
                var cnmt = ncaFile.GetCnmtFromNca(keySet);

                if (cnmt == null)
                    continue;

                string titleId = cnmt.TitleId.ToString("X16");
                uint version = cnmt.TitleVersion.Version;
                ContentMetaType type = cnmt.Type;

                var contentNcaIds = cnmt.ContentEntries
                    .Select(e => Convert.ToHexStringLower(e.NcaId))
                    .ToList();

                string selfNcaId = System.IO.Path.GetFileNameWithoutExtension(System.IO.Path.GetFileNameWithoutExtension(cnmtNcaName));

                if (!string.IsNullOrEmpty(selfNcaId))
                    contentNcaIds.Add(selfNcaId.ToLowerInvariant());

                long calculatedSize = 0;

                foreach (var ncaId in contentNcaIds)
                {
                    var matchedKey = allFiles.Keys.FirstOrDefault(k => k.StartsWith(ncaId, StringComparison.OrdinalIgnoreCase));

                    if (matchedKey != null)
                    {
                        try
                        {
                            using var targetFile = allFiles[matchedKey].AsFile(OpenMode.Read);

                            targetFile.GetSize(out long size);

                            calculatedSize += size;
                        }
                        catch { }
                    }
                }

                try
                {
                    ncaFile.GetSize(out long size);

                    calculatedSize += size;
                }
                catch { }

                string krTitle = string.Empty;
                string enTitle = string.Empty;
                string koreanTitle = string.Empty;
                string defaultTitle = string.Empty;
                string displayVer = "1.0.0";
                byte[]? iconData = null;
                var ctrlRecord = cnmt.ContentEntries
                    .FirstOrDefault(x => x.Type == LibHac.Ncm.ContentType.Control);

                if (ctrlRecord != null)
                {
                    string ctrlId = Convert.ToHexStringLower(ctrlRecord.NcaId);
                    string? ctrlName = allFiles.Keys.FirstOrDefault(k => k.StartsWith(ctrlId, StringComparison.OrdinalIgnoreCase));

                    if (ctrlName != null)
                    {
                        try
                        {
                            using var ctrlFile = allFiles[ctrlName].AsFile(OpenMode.Read);

                            ctrlFile.GetSize(out long size);

                            calculatedSize += size;
                        }
                        catch { }

                        var control = allFiles[ctrlName].GetControlProperty(keySet);

                        if (control != null)
                        {
                            var current = control.Value;

                            krTitle = current.Title[(int)Current].NameString.ToString().Trim('\0', ' ');
                            enTitle = current.Title[(int)Language.AmericanEnglish].NameString.ToString().Trim('\0', ' ');
                            koreanTitle = current.Title[(int)Language.Korean].NameString.ToString().Trim('\0', ' ');

                            if (string.IsNullOrWhiteSpace(krTitle))
                            {
                                foreach (ApplicationTitle t in current.Title)
                                {
                                    var name = t.NameString.ToString().Trim('\0', ' ');

                                    if (!string.IsNullOrWhiteSpace(name))
                                    {
                                        krTitle = name;
                                        break;
                                    }
                                }
                            }

                            if (string.IsNullOrWhiteSpace(enTitle))
                                enTitle = krTitle;

                            defaultTitle = koreanTitle;

                            if (string.IsNullOrWhiteSpace(defaultTitle))
                            {
                                foreach (ApplicationTitle t in current.Title)
                                {
                                    var name = t.NameString.ToString().Trim('\0', ' ');

                                    if (!string.IsNullOrWhiteSpace(name))
                                    {
                                        defaultTitle = name;
                                        break;
                                    }
                                }
                            }

                            displayVer = current.DisplayVersionString.ToString().Trim('\0', ' ');

                            if (readIcon)
                                iconData = ReadControlIcon(allFiles[ctrlName], keySet, current.GetTitleByLanguage(Current).Language);
                        }
                    }
                }

                var programIdOffsets = cnmt.ContentEntries
                    .Where(e => e.Type == LibHac.Ncm.ContentType.Program)
                    .Select(e => e.IdOffset)
                    .Distinct()
                    .OrderBy(o => o)
                    .ToList();

                bool hasCompressedRomfs = false;

                foreach (var programEntry in cnmt.ContentEntries.Where(e => e.Type == LibHac.Ncm.ContentType.Program))
                {
                    string programNcaId = Convert.ToHexStringLower(programEntry.NcaId);
                    string? programName = allFiles.Keys.FirstOrDefault(k => k.StartsWith(programNcaId, StringComparison.OrdinalIgnoreCase));

                    if (programName != null && RomfsCompression.HasCompressedSection(OpenNca(allFiles[programName], programName, keySet)))
                    {
                        hasCompressedRomfs = true;
                        break;
                    }
                }

                results.Add(new MetadataResult(titleId, version, displayVer, krTitle, enTitle, 0, type, cnmtNcaName, path, contentNcaIds, programIdOffsets)
                {
                    IconData = iconData,
                    Size = calculatedSize,
                    KoreanTitle = koreanTitle,
                    DefaultTitle = defaultTitle,
                    HasCompressedRomfs = hasCompressedRomfs
                });
            }
            catch
            {
                throw;
            }
        }

        foreach (var s in allFiles.Values)
            s.Dispose();

        if (fs is IDisposable d)
            d.Dispose();

        return results;
    }

    private static Nca? OpenNca(IStorage storage, string fileName, KeySet keySet)
    {
        try
        {
            return fileName.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase) ? new Ncz(keySet, storage, NczReadMode.Fast) : new Nca(keySet, storage);
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? ReadControlIcon(IStorage ncaStorage, KeySet keySet, Language language)
    {
        try
        {
            var nca = new Nca(keySet, ncaStorage);
            using var ctrlFs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);
            return ctrlFs.GetIconData(language);
        }
        catch
        {
            return null;
        }
    }

    public static GameFileInfo ReadGameFileInfo(KeySet keySet, string path)
    {
        var info = new GameFileInfo { Path = path };

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return info;

        var metas = GetMetadataFromContainer(keySet, path, readIcon: true);

        if (metas.Count == 0)
            return info;

        var types = new List<string>();

        if (metas.Any(m => m.Type == ContentMetaType.Application))
            types.Add("B");

        if (metas.Any(m => m.Type == ContentMetaType.Patch))
            types.Add("U");

        if (metas.Any(m => m.Type is ContentMetaType.AddOnContent or ContentMetaType.Delta))
            types.Add("D");

        var main = metas.FirstOrDefault(m => m.Type == ContentMetaType.Patch) ?? metas.FirstOrDefault(m => m.Type == ContentMetaType.Application) ?? metas[0];

        info.Type = types.Count > 0 ? string.Join("+", types) : "?";
        info.TitleId = main.TitleId;
        info.DisplayVersion = main.GetEffectiveDisplayVersion();
        info.TitleName = !string.IsNullOrEmpty(main.KrTitle) ? main.KrTitle : main.EnTitle;
        info.IconData = main.IconData;
        info.Naming = GameNamingInfo.From(metas);

        return info;
    }

    public static List<Cnmt> GetCnmtsFromContainer(KeySet keySet, string path)
    {
        var results = new List<Cnmt>();

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return results;

        using var storage = new LocalStorage(path, FileAccess.Read);
        using var fs = storage.OpenFileSystem(keySet, path);

        foreach (var entry in fs.EnumerateEntries("/", "*.nc*")
            .Where(e => e.Type == DirectoryEntryType.File && (e.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) || e.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase))))
        {
            try
            {
                using var file = new UniqueRef<IFile>();

                if (fs.OpenFile(ref file.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).IsFailure())
                    continue;

                var nca = new Nca(keySet, file.Release().AsStorage());

                if (nca.Header.ContentType != NcaContentType.Meta)
                    continue;

                using var metaFs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);

                foreach (var cnmtEntry in metaFs.EnumerateEntries("/", "*.cnmt"))
                {
                    using var cnmtFile = new UniqueRef<IFile>();

                    if (metaFs.OpenFile(ref cnmtFile.Ref, cnmtEntry.FullPath.ToU8Span(), OpenMode.Read).IsFailure())
                        continue;

                    results.Add(new Cnmt(cnmtFile.Get.AsStream()));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CNMT 파싱 실패 ({entry.FullPath}): {ex.Message}");
            }
        }

        return results;
    }

    public static string GetContentMetaTypeTag(ContentMetaType type) => type switch
    {
        ContentMetaType.Application => "Base",
        ContentMetaType.Patch => "Update",
        ContentMetaType.AddOnContent => "DLC",
        ContentMetaType.Delta => "DLC",
        _ => "?"
    };

    private static (string Name, string Publisher, Language Language) GetTitleByLanguage(this ApplicationControlProperty control, Language preferred)
    {
        int langCount = control.TitleCompression == TitleCompressionValue.Enable ? Constants.ExtendedLanguageCount : Constants.LegacyLanguageCount;
        var titles = control.Title;
        var title = titles[(int)preferred];

        if (!string.IsNullOrWhiteSpace(title.NameString.ToString().Trim('\0')))
            return (title.NameString.ToString().Trim('\0'), title.PublisherString.ToString().Trim('\0'), preferred);

        title = titles[(int)Language.AmericanEnglish];

        if (!string.IsNullOrWhiteSpace(title.NameString.ToString().Trim('\0')))
            return (title.NameString.ToString().Trim('\0'), title.PublisherString.ToString().Trim('\0'), Language.AmericanEnglish);

        for (int i = 0; i < langCount; i++)
        {
            var t = titles[i];
            var name = t.NameString.ToString().Trim('\0');

            if (!string.IsNullOrWhiteSpace(name))
                return (name, t.PublisherString.ToString().Trim('\0'), (Language)i);
        }

        return ("Unknown", "Unknown", preferred);
    }
}