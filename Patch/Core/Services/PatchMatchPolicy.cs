using Patch.Core.Enums;

namespace Patch.Core.Services;

public static class PatchMatchPolicy
{
    public static bool AllowsLooseNameMatch(PatchFormat format) => format is PatchFormat.Xdelta or PatchFormat.Ups or PatchFormat.Bps;
}