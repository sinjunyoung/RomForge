using WiiU.Core.Services;

namespace WiiU.Core.Models;

public sealed record RepackEntry(ITitleSource Source, string? PatchFolder = null, string? TitleIdHexOverride = null, int? TitleVersionOverride = null);