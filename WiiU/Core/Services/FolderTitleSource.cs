using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace WiiU.Core.Services;

public sealed partial class FolderTitleSource : ITitleSource
{
    [GeneratedRegex(@"^([0-9a-fA-F]{16})_v(\d+)$")]
    private static partial Regex HexVersionRegex();

    private readonly string _root;

    public string TitleIdHex { get; private set; } = "0000000000000000";

    public int TitleVersion { get; private set; }

    public FolderTitleSource(string rootFolder)
    {
        _root = rootFolder;

        string metaXmlPath = Path.Combine(rootFolder, "meta", "meta.xml");

        if (File.Exists(metaXmlPath))
        {
            try
            {
                var doc = XDocument.Load(metaXmlPath);
                string? titleId = doc.Root?.Element("title_id")?.Value?.Trim();
                string? titleVersion = doc.Root?.Element("title_version")?.Value?.Trim();

                if (!string.IsNullOrEmpty(titleId))
                {
                    TitleIdHex = titleId.ToLowerInvariant();

                    if (!string.IsNullOrEmpty(titleVersion) && int.TryParse(titleVersion, out int v))
                        TitleVersion = v;
                }
            }
            catch { }
        }

        if (TitleIdHex == "0000000000000000")
        {
            string name = new DirectoryInfo(rootFolder).Name;
            var match = HexVersionRegex().Match(name);

            if (match.Success)
            {
                TitleIdHex = match.Groups[1].Value.ToLowerInvariant();
                TitleVersion = int.Parse(match.Groups[2].Value);
            }
        }
    }

    public IEnumerable<string> EnumerateFiles()
    {
        foreach (string file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            yield return Path.GetRelativePath(_root, file).Replace(Path.DirectorySeparatorChar, '/');
    }

    public Stream OpenRead(string path) => File.OpenRead(Path.Combine(_root, path.Replace('/', Path.DirectorySeparatorChar)));

    public long GetFileSize(string path)
    {
        string full = Path.Combine(_root, path.Replace('/', Path.DirectorySeparatorChar));

        return File.Exists(full) ? new FileInfo(full).Length : 0;
    }

    public void Dispose() { }
}