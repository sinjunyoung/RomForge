using Common.Localization;

namespace RomForge.Core.Models.Switch;

public static class SwitchFileTypes
{
    public static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".nsp", ".xci", ".nsz", ".xcz" };

    public static string OpenFileFilter => $"{Loc.S.FilterSwitchFiles} (*.nsp;*.xci;*.nsz;*.xcz)|*.nsp;*.xci;*.nsz;*.xcz|{Loc.S.FilterAllFiles}|*.*";
}