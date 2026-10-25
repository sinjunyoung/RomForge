using WiiGC.Core.Services.Wii;

namespace WiiGC.Core.Models.Wii;

internal sealed record RepackPlan(RepackedPartition Data, long DataSize, IReadOnlyList<PatchEntry> Entries);