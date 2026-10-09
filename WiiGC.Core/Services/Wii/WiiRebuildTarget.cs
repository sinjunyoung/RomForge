using WiiGC.Core.Models;

namespace WiiGC.Core.Services.Wii;

internal sealed record WiiRebuildTarget(int Index, WiiPartitionSpec Spec, WiiRepackPlan Plan);
