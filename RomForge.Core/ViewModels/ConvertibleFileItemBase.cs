namespace RomForge.Core.ViewModels;

public abstract class ConvertibleFileItemBase : FileItemBase, Interfaces.IConvertible
{
    private string _selectedTargetFormat = string.Empty;

    public List<string> AvailableFormats { get; } = [];

    public string SelectedTargetFormat
    {
        get => _selectedTargetFormat;
        set => SetProperty(ref _selectedTargetFormat, value);
    }

    protected ConvertibleFileItemBase(string filePath, string unsupportedLabel = "미지원") : base(filePath)
    {
        var formats = GetAvailableFormats(Extension);

        AvailableFormats.AddRange(formats);
        SelectedTargetFormat = formats.Count > 0 ? formats[0] : unsupportedLabel;
    }

    protected abstract IReadOnlyList<string> GetAvailableFormats(string extension);
}