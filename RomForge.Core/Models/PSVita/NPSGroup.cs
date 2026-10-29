namespace RomForge.Core.Models.PSVita;

public class NPSGroup
{
    public string? TitleId { get; set; }

    public NPSItem? App { get; set; }

    public List<NPSItem> Dlcs { get; set; } = [];

    public string? GetDlcLicense(string? contentId)
    {
        if (string.IsNullOrEmpty(contentId) || Dlcs == null)
            return null;

        var dlcItem = Dlcs.FirstOrDefault(d => string.Equals(d.ContentId, contentId, StringComparison.OrdinalIgnoreCase));

        return dlcItem?.ZRif;
    }
}