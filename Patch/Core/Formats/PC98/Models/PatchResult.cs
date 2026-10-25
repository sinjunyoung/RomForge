namespace Patch.Core.Formats.PC98.Models;

public sealed record PatchResult(int AppliedCount, IReadOnlyList<string> MissingFiles, string MountPoint, string PatchSourceName);