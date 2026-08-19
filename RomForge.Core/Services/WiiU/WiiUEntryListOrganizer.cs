using RomForge.Core.Models.WiiU;
using System.Collections.ObjectModel;

namespace RomForge.Core.Services.WiiU;

public static class WiiUEntryListOrganizer
{
    public static void Reorganize(ObservableCollection<TitleInputEntry> entries)
    {
        var baseRow = entries.FirstOrDefault(e => e.Role == TitleRole.Base);
        var updateRow = entries.FirstOrDefault(e => e.Role == TitleRole.Update);
        var dlcRows = entries
            .Where(e => e.Role == TitleRole.Dlc)
            .OrderBy(e => e.TitleIdHex, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var placed = new HashSet<TitleInputEntry>();

        if (baseRow != null)
            placed.Add(baseRow);

        if (updateRow != null)
            placed.Add(updateRow);

        foreach (var d in dlcRows)
            placed.Add(d);

        var others = entries.Where(e => !placed.Contains(e)).ToList();

        var ordered = new List<TitleInputEntry>();

        if (baseRow != null)
            ordered.Add(baseRow);

        if (updateRow != null) 
            ordered.Add(updateRow);

        ordered.AddRange(dlcRows);
        ordered.AddRange(others);

        if (ordered.SequenceEqual(entries))
            return;

        entries.Clear();

        foreach (var e in ordered)
            entries.Add(e);
    }
}