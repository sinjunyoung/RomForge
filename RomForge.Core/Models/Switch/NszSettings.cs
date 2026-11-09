using Common.Models;

namespace RomForge.Core.Models.Switch;

public sealed record NszSettings(int CompressLevel, SwitchCompressionMode CompressionMode);