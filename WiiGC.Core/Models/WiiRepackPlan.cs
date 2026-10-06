using WiiGC.Core.Services.Wii;

namespace WiiGC.Core.Models;

internal sealed record WiiRepackPlan(WiiRepackedPartition Data, long DataSize, IReadOnlyList<WiiPatchEntry> Entries);