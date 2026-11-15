using RomForge.Core.Models.PSP;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;

namespace RomForge.Core.Services.PSP;

public static partial class DiscListSorter
{
    [GeneratedRegex(@"\s*\(Disc\s*\d+\)", RegexOptions.IgnoreCase)]
    private static partial Regex DiscNumberRegex();

    public static void SortAndRenumber(ObservableCollection<DiscFileItem> items)
    {
        var sorted = items
            .OrderBy(i => i.IsGameIdResolved ? 0 : 1)
            .ThenBy(i => i.GameId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var i = 0; i < sorted.Count; i++)
        {
            sorted[i].No = i + 1;

            var oldIndex = items.IndexOf(sorted[i]);

            if (oldIndex != i)
                items.Move(oldIndex, i);
        }
    }

    public static string GuessTitle(string filePath) => DiscNumberRegex().Replace(Path.GetFileNameWithoutExtension(filePath), string.Empty).Trim();
}