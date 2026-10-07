namespace NSW.Core.Models;

public class GameFileInfo
{
    public string Path { get; set; } = string.Empty;

    public string TitleName { get; set; } = string.Empty;

    public string TitleId { get; set; } = "0000000000000000";

    public string DisplayVersion { get; set; } = "0.0.0";

    public string Type { get; set; } = "?";

    public string Developer { get; set; } = string.Empty;

    public byte[]? IconData { get; set; }
}