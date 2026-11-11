namespace RomForge.Core.ViewModels;

public abstract class ConvertibleFileItemBase : FileItemBase, Interfaces.IConvertible
{
    public List<string> AvailableFormats { get; } = [];

    public string SelectedTargetFormat { get; set => SetProperty(ref field, value); } = string.Empty;

    protected ConvertibleFileItemBase(string filePath) : base(filePath)
    {
        var formats = GetAvailableFormats(Extension);

        AvailableFormats.AddRange(formats);
        SelectedTargetFormat = formats.Count > 0 ? formats[0] : string.Empty;
    }

    protected abstract IReadOnlyList<string> GetAvailableFormats(string extension);
}