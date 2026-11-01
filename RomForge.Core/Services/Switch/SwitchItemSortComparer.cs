using RomForge.Core.ViewModels.Switch;
using System.Collections;
using System.Globalization;

namespace RomForge.Core.Services.Switch;

public class SwitchItemSortComparer : IComparer
{
    public int Compare(object? x, object? y)
    {
        if (x is not GameFile f1 || y is not GameFile f2)
            return 0;

        string id1 = f1.TitleID ?? string.Empty;
        string id2 = f2.TitleID ?? string.Empty;
        string base1 = id1.Length >= 12 ? id1[..12] : id1;
        string base2 = id2.Length >= 12 ? id2[..12] : id2;

        int baseCompare = string.Compare(base1, base2, StringComparison.OrdinalIgnoreCase);

        if (baseCompare != 0)
            return baseCompare;

        int p1 = GetPriority(id1);
        int p2 = GetPriority(id2);

        if (p1 != p2)
            return p1.CompareTo(p2);

        return string.Compare(id1, id2, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetPriority(string titleId)
    {
        if (string.IsNullOrEmpty(titleId) || titleId.Length < 3)
            return 999;

        string suffix = titleId[^3..];

        if (suffix.Equals("000", StringComparison.OrdinalIgnoreCase))
            return 1;

        if (suffix.Equals("800", StringComparison.OrdinalIgnoreCase))
            return 2;

        if (int.TryParse(suffix, NumberStyles.HexNumber, null, out int val) && val > 0 && val != 0x800)
            return 3 + val;

        return 999;
    }
}