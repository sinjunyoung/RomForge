using Patch.Core.Enums;
using Patch.Core.Services;

namespace Patch.Core.Models;

public record PatchFileEntry(PatchFileRef File, string RelativeDir, string BaseName, PatchFileKind Kind);