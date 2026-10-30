using RomForge.Core.Models.PSVita;
using System.IO;

namespace RomForge.Core.Services.PSVita
{
    public class NPSTsvParser
    {
        private static readonly Lazy<NPSTsvParser> _instance = new(() => new NPSTsvParser());

        public static NPSTsvParser Instance => _instance.Value;

        private readonly Dictionary<string, NPSGroup> _cachedData = new(StringComparer.OrdinalIgnoreCase);

        private NPSTsvParser() { }

        public void LoadData(string gamesTsvPath, string dlcsTsvPath)
        {
            _cachedData.Clear();

            if (File.Exists(gamesTsvPath))
                ParseTsvFile(gamesTsvPath, isDlc: false, _cachedData);

            if (File.Exists(dlcsTsvPath))
                ParseTsvFile(dlcsTsvPath, isDlc: true, _cachedData);
        }

        public bool TryGetGroup(string titleId, out NPSGroup group) => _cachedData.TryGetValue(titleId, out group!);

        private static void ParseTsvFile(string filePath, bool isDlc, Dictionary<string, NPSGroup> dictionary)
        {
            using var reader = new StreamReader(filePath);
            string? headerLine = reader.ReadLine();

            if (string.IsNullOrWhiteSpace(headerLine))
                return;

            string[] headers = headerLine.Split('\t');
            int titleIdIdx = Array.IndexOf(headers, "Title ID");
            int zRifIdx = Array.IndexOf(headers, "zRIF");
            int contentIdIdx = Array.IndexOf(headers, "Content ID");

            if (titleIdIdx == -1 || zRifIdx == -1 || contentIdIdx == -1)
                throw new Exception($"필수 헤더를 {filePath}에서 찾을 수 없습니다.");

            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] parts = line.Split('\t');

                if (parts.Length <= Math.Max(titleIdIdx, Math.Max(zRifIdx, contentIdIdx)))
                    continue;

                string titleId = parts[titleIdIdx].Trim();
                string zRif = parts[zRifIdx].Trim();
                string rawContentId = parts[contentIdIdx].Trim();

                if (string.IsNullOrEmpty(titleId))
                    continue;

                string contentId = rawContentId;
                int lastHyphenIdx = rawContentId.LastIndexOf('-');

                if (lastHyphenIdx >= 0 && lastHyphenIdx < rawContentId.Length - 1)
                    contentId = rawContentId[(lastHyphenIdx + 1)..];

                var item = new NPSItem
                {
                    TitleId = titleId,
                    ZRif = zRif,
                    ContentId = contentId,
                    IsDlc = isDlc
                };

                if (!dictionary.TryGetValue(titleId, out var group))
                {
                    group = new NPSGroup { TitleId = titleId };
                    dictionary[titleId] = group;
                }

                if (!isDlc)
                    group.App = item;
                else
                    group.Dlcs.Add(item);
            }
        }
    }
}