using N3DS.Core.Crypto;
using N3DS.Core.Models;

namespace N3DS.Core.FileSystem;

public class SdTitleScanner
{
    private readonly SdCrypto _sdCrypto;
    private readonly KeyStore _keyStore;

    public SdTitleScanner(string sdRoot, KeyStore keyStore, SdCrypto sdCrypto)
    {
        SdRoot = sdRoot ?? throw new ArgumentNullException(nameof(sdRoot));
        _keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
        _sdCrypto = sdCrypto ?? throw new ArgumentNullException(nameof(sdCrypto));

        Id1Path = FindId1Path();
    }

    private string FindId1Path()
    {
        string id0Hex = _keyStore.GetId0Hex();
        string id0Path = Path.Combine(SdRoot, "Nintendo 3DS", id0Hex);

        if (!Directory.Exists(id0Path))
            throw new DirectoryNotFoundException($"id0 디렉토리를 찾을 수 없음: {id0Path}");

        var id1Dirs = Directory.GetDirectories(id0Path)
            .Where(d => IsHex32(Path.GetFileName(d)))
            .ToList();

        if (id1Dirs.Count == 0)
            throw new DirectoryNotFoundException($"id1 디렉토리를 찾을 수 없음");

        if (id1Dirs.Count > 1)
            throw new InvalidOperationException($"id1 디렉토리가 여러 개 존재함: {string.Join(", ", id1Dirs.Select(Path.GetFileName))}");

        return id1Dirs[0];
    }

    public string Id1Path { get; internal set; }

    public string SdRoot { get; internal set; }

    public List<InstalledTitle> ScanTitles(IProgress<string>? progress = null)
    {
        string titleRoot = Path.Combine(Id1Path, "title");

        if (!Directory.Exists(titleRoot))
            return [];

        var result = new List<InstalledTitle>();

        foreach (var highDir in Directory.GetDirectories(titleRoot))
        {
            string tidHigh = Path.GetFileName(highDir).ToLowerInvariant();

            if (tidHigh.Length != 8 || !IsHex(tidHigh))
                continue;

            foreach (var lowDir in Directory.GetDirectories(highDir))
            {
                string tidLow = Path.GetFileName(lowDir).ToLowerInvariant();

                if (tidLow.Length != 8 || !IsHex(tidLow))
                    continue;

                string titleId = tidHigh + tidLow;

                progress?.Report($"스캔 중: {titleId}");

                try
                {
                    var title = ReadTitle(lowDir, tidHigh, tidLow, titleId);

                    if (title is not null)
                        result.Add(title);
                }
                catch (Exception ex)
                {
                    progress?.Report($"오류 ({titleId}): {ex.Message}");
                }
            }
        }

        return result;
    }

    private InstalledTitle? ReadTitle(string titleDir, string tidHigh, string tidLow, string titleId)
    {
        string contentDir = Path.Combine(titleDir, "content");

        if (!Directory.Exists(contentDir))
            return null;

        string? tmdFile = Directory.GetFiles(contentDir, "*.tmd").FirstOrDefault();

        if (tmdFile is null)
            return null;

        string sdTmdPath = ToSdPath(tmdFile);
        byte[] encrypted = File.ReadAllBytes(tmdFile);        
        byte[] decrypted = _sdCrypto.Decrypt(sdTmdPath, encrypted);
        var tmd = TmdParser.Parse(decrypted);
        string dataDir = Path.Combine(titleDir, "data");
        bool hasSave = File.Exists(Path.Combine(dataDir, "00000001.sav"));

        return new InstalledTitle
        {
            TitleId = titleId,
            TitleIdHigh = tidHigh,
            TitleIdLow = tidLow,
            Type = tmd.TitleType,
            Version = tmd.TitleVersion,
            SaveSize = tmd.SaveSize,
            ContentSize = tmd.TotalContentSize,
            Contents = tmd.Contents,
            TitlePath = titleDir,
            ContentPath = contentDir,
            HasSave = hasSave,
        };
    }

    private string ToSdPath(string absolutePath)
    {
        string id1 = Id1Path.TrimEnd('\\', '/');
        string relative = absolutePath.StartsWith(id1, StringComparison.OrdinalIgnoreCase) ? absolutePath[id1.Length..] : absolutePath;
        return relative.Replace('\\', '/');
    }

    private static bool IsHex32(string? s) => s?.Length == 32 && IsHex(s);

    private static bool IsHex(string s) => s.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
}