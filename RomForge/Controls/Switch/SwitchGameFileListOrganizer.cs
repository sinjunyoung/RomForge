using NSW.WPF.ViewModels;
using System.Collections.ObjectModel;

namespace RomForge.Controls.Switch;

public static class SwitchGameFileListOrganizer
{
    public static void Reorganize(ObservableCollection<GameFile> gameFiles)
    {
        var baseRow = gameFiles.FirstOrDefault(f => f.FileType.Contains('B'));
        var updateRow = gameFiles.FirstOrDefault(f => f.FileType.Contains('U') && !ReferenceEquals(f, baseRow));
        var dlcRows = gameFiles
            .Where(f => f.FileType.Contains('D'))
            .OrderBy(f => f.TitleID, StringComparer.OrdinalIgnoreCase)
            .ToList();
        int dlcIndexWidth = dlcRows.Count.ToString().Length;

        for (int i = 0; i < dlcRows.Count; i++)
            dlcRows[i].FileType = $"D{(i + 1).ToString().PadLeft(dlcIndexWidth, '0')}";

        var placed = new HashSet<GameFile>();

        if (baseRow != null)
            placed.Add(baseRow);

        if (updateRow != null) 
            placed.Add(updateRow);

        foreach (var d in dlcRows) 
            placed.Add(d);

        var others = gameFiles.Where(f => !placed.Contains(f)).ToList();

        var ordered = new List<GameFile>();

        if (baseRow != null) 
            ordered.Add(baseRow);

        if (updateRow != null) 
            ordered.Add(updateRow);

        ordered.AddRange(dlcRows);
        ordered.AddRange(others);

        if (ordered.SequenceEqual(gameFiles))
            return;

        gameFiles.Clear();

        foreach (var g in ordered)
            gameFiles.Add(g);
    }
}