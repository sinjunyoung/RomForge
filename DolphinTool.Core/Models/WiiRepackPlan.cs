using DolphinTool.Core.Services.Wii;

namespace DolphinTool.Core.Models;

internal sealed record WiiRepackPlan(WiiRepackedPartition Data, long DataSize, int Replaced);