using Patch.Core.Enums;
using Patch.Core.Services;

namespace Patch.Core.Models;

public sealed record PlannedPatch(PatchFileRef File, PatchFormat Format, bool Strict);