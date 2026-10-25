using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Ns;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.HacPack.Models;
using System.IO;
using static LibHac.Ns.ApplicationControlProperty;
using Path = System.IO.Path;

namespace NSW.Core.Services;

public static class MetadataService
{
    public static GameMetadata? GetGameMetadata(KeySet keySet, List<string> allPaths)
    {
        var candidates = new List<GameMetadata>();

        foreach (var path in allPaths)
        {
            try
            {
                using var storage = new LocalStorage(path, FileAccess.Read);
                using IFileSystem fs = storage.OpenFileSystem(keySet, path);
                uint maxVersionInFile = 0;
                ContentMetaType bestTypeInFile = ContentMetaType.Application;
                List<LanguageInfo> bestLangsInFile = [];
                List<byte> bestIndicesInFile = [];
                var entries = fs.EnumerateEntries("/", "*.nca").ToList();

                foreach (var entry in entries)
                {
                    using var file = new UniqueRef<IFile>();

                    if (fs.OpenFile(ref file.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).IsFailure())
                        continue;

                    var ncaStorage = file.Release().AsStorage();
                    var nca = new Nca(keySet, ncaStorage);

                    if (nca.Header.ContentType == NcaContentType.Meta)
                    {
                        var cnmt = ncaStorage.GetCnmtFromNca(keySet);

                        if (cnmt != null && cnmt.Type != ContentMetaType.AddOnContent)
                        {
                            if (cnmt.TitleVersion.Version >= maxVersionInFile)
                            {
                                maxVersionInFile = cnmt.TitleVersion.Version;
                                bestTypeInFile = cnmt.Type;
                                bestIndicesInFile = [.. cnmt.ContentEntries
                                    .Where(e => e.Type == LibHac.Ncm.ContentType.Program)
                                    .Select(e => e.IdOffset)
                                    .Distinct()
                                    .OrderBy(i => i)];
                            }
                        }
                    }
                    else if (nca.Header.ContentType == NcaContentType.Control)
                    {
                        using var cFs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);

                        if (nca.GetControlProperty() is { } control)
                        {
                            var titles = control.Title;
                            bool isCompressed = control.TitleCompression == TitleCompressionValue.Enable;
                            int langCount = isCompressed ? Constants.ExtendedLanguageCount : Constants.LegacyLanguageCount;
                            var currentFileLangs = new List<LanguageInfo>();

                            for (int i = 0; i < langCount; i++)
                            {
                                currentFileLangs.Add(new LanguageInfo
                                {
                                    Language = (Language)i,
                                    Flag = (control.SupportedLanguageFlag & (1u << i)) != 0,
                                    TitleName = titles[i].NameString.ToString().Trim('\0', ' '),
                                    Publisher = titles[i].PublisherString.ToString().Trim('\0', ' '),
                                    LogoData = cFs.GetIconData((Language)i)
                                });
                            }

                            bestLangsInFile = currentFileLangs;
                        }
                    }
                }

                if (bestLangsInFile.Count > 0)
                    candidates.Add(new GameMetadata
                    {
                        Languages = bestLangsInFile,
                        Type = bestTypeInFile,
                        Version = maxVersionInFile,
                        Indices = bestIndicesInFile
                    });
            }
            catch 
            { 
                continue;
            }
        }

        return candidates
            .OrderByDescending(c => c.Type == ContentMetaType.Patch)
            .ThenByDescending(c => c.Version)
            .FirstOrDefault();
    }

    public static GameMetadata? GetGameMetadataFromUnpacked(string unpackedDir)
    {
        string? baseDir = LibHacHelper.FindBaseProgramDir(unpackedDir);

        if (baseDir == null)
            return null;

        string nacpPath = Path.Combine(baseDir, "control", "control.nacp");

        if (!File.Exists(nacpPath))
            return null;

        var langs = new List<LanguageInfo>();

        try
        {
            byte[] nacpData = File.ReadAllBytes(nacpPath);
            var control = new ApplicationControlProperty();

            nacpData.AsSpan().CopyTo(SpanHelpers.AsByteSpan(ref control));

            var titles = control.Title;
            bool isCompressed = control.TitleCompression == TitleCompressionValue.Enable;
            int langCount = isCompressed ? Constants.ExtendedLanguageCount : Constants.LegacyLanguageCount;

            for (int i = 0; i < langCount; i++)
            {
                string iconPath = Path.Combine(baseDir, "control", $"icon_{(Language)i}.dat");
                byte[]? iconData = File.Exists(iconPath) ? File.ReadAllBytes(iconPath) : null;

                langs.Add(new LanguageInfo
                {
                    Language = (Language)i,
                    Flag = (control.SupportedLanguageFlag & (1u << i)) != 0,
                    TitleName = titles[i].NameString.ToString().Trim('\0', ' '),
                    Publisher = titles[i].PublisherString.ToString().Trim('\0', ' '),
                    LogoData = iconData
                });
            }
        }
        catch { }

        return new GameMetadata
        {
            Languages = langs,
            Type = ContentMetaType.Application,
            Version = 0,
            Indices = GetMultiRomIndicesFromUnpacked(unpackedDir)
        };
    }

    private static List<byte> GetMultiRomIndicesFromUnpacked(string unpackedDir)
    {
        var indices = new List<byte>();

        if (!Directory.Exists(unpackedDir))
            return indices;

        foreach (var dir in Directory.EnumerateDirectories(unpackedDir))
        {
            string name = Path.GetFileName(dir);
            byte? idOffset = name == "control" ? (byte)0 : name.StartsWith("control") && byte.TryParse(name["control".Length..], out var n) ? n : null;

            if (idOffset is byte offset && File.Exists(Path.Combine(dir, "control.nacp")))
                indices.Add(offset);
        }

        indices.Sort();
        return indices;
    }
}