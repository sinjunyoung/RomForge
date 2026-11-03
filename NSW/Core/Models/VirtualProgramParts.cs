using NSW.Core.Services;

namespace NSW.Core.Models;

public sealed class VirtualProgramParts
{
    public string ProgramId { get; set; } = string.Empty;

    public VirtualRomfs? Exefs { get; set; }

    public VirtualRomfs? Logo { get; set; }

    public VirtualRomfs? Control { get; set; }

    public VirtualRomfs? HtmlDoc { get; set; }

    public VirtualRomfs? Legal { get; set; }
}