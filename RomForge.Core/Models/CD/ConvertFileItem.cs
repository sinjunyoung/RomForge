using CD.Core.Services.Readers;
using RomForge.Core.ViewModels;
using System.IO;
using System.Windows.Media;

namespace RomForge.Core.Models.CD;

public class ConvertFileItem(string filePath) : FileItemBase(filePath), Interfaces.IConvertible
{
    public SourceFormat SourceFormat => Extension switch
    {
        "mds" => SourceFormat.MdfMds,
        "ccd" => SourceFormat.CcdImgSub,
        _ => SourceFormat.Unknown
    };

    public int TrackCount
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(IsMultiTrack));
                OnPropertyChanged(nameof(AvailableOutputFormats));
                OnPropertyChanged(nameof(AvailableFormats));

                if (IsMultiTrack && OutputFormat == OutputFormat.Iso)
                    OutputFormat = OutputFormat.BinCue;
            }
        }
    }

    public bool IsMultiTrack => TrackCount > 1;

    public IReadOnlyList<OutputFormat> AvailableOutputFormats => IsMultiTrack ? [OutputFormat.BinCue] : [OutputFormat.BinCue, OutputFormat.Iso];

    public OutputFormat OutputFormat
    {
        get;
        set
        {
            if (value == OutputFormat.Iso && IsMultiTrack)
                value = OutputFormat.BinCue;

            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(ExtensionLabel));
                OnPropertyChanged(nameof(SelectedTargetFormat));
            }
        }
    } = OutputFormat.BinCue;

    public string ExtensionLabel => SourceFormat switch
    {
        SourceFormat.MdfMds or SourceFormat.CcdImgSub => $"{Extension}→{(_wantsChd ? "chd" : OutputFormat == OutputFormat.Iso ? "iso" : "cue")}",
        _ => Extension
    };

    public List<string> AvailableFormats => Extension == "ccd" ? [.. AvailableOutputFormats.Select(f => f == OutputFormat.Iso ? "ISO" : "CUE"), "CHD"] : [.. AvailableOutputFormats.Select(f => f == OutputFormat.Iso ? "ISO" : "CUE")];

    private bool _wantsChd;

    public string SelectedTargetFormat
    {
        get => _wantsChd ? "CHD" : OutputFormat == OutputFormat.Iso ? "ISO" : "CUE";
        set
        {
            _wantsChd = value == "CHD";

            if (!_wantsChd)
                OutputFormat = value == "ISO" ? OutputFormat.Iso : OutputFormat.BinCue;

            OnPropertyChanged();
            OnPropertyChanged(nameof(ExtensionLabel));
        }
    }

    public Brush ExtensionBackground => ExtensionColorMap.Resolve(Extension, ColorMap);

    private static readonly Dictionary<string, string> ColorMap = new()
    {
        ["mds"] = "#EAE2A6",
        ["mdf"] = "#D2DAA5",
        ["ccd"] = "#A6C8E2",
    };

    protected override string FormatSize(long bytes) => PickPack.Disk.Helpers.FileSize.FormatSize(bytes);

    protected override long CalculateSize(string filePath)
    {
        var selfSize = new FileInfo(filePath).Length;
        var dir = Directory;

        static long SumExisting(IEnumerable<string> paths) => paths.Where(File.Exists).Sum(p => new FileInfo(p).Length);

        return Extension switch
        {
            "mds" => selfSize + SumExisting(MdfMdsReader.GetReferencedFileNames(filePath)
                .Select(name => name == "*.mdf"
                    ? Path.ChangeExtension(filePath, ".mdf")
                    : Path.Combine(dir, name))),

            "ccd" => selfSize + SumExisting([
                Path.ChangeExtension(filePath, ".img"),
                Path.ChangeExtension(filePath, ".sub")
            ]),

            _ => base.CalculateSize(filePath)
        };
    }
}