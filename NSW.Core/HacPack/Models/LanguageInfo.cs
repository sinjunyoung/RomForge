using static LibHac.Ns.ApplicationControlProperty;

namespace NSW.HacPack.Models;

public class LanguageInfo
{
    public bool Flag { get; set; }

    public Language Language { get; set; }

    public string TitleName { get; set; } = string.Empty;

    public string Publisher { get; set; } = string.Empty;

    public byte[]? LogoData { get; set; }
}