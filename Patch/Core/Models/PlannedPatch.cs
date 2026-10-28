using Patch.Core.Services;

namespace Patch.Core.Models;

public sealed record PlannedPatch(PatchFileRef File, bool Strict);