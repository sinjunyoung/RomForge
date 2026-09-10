using CD.Core.Services.Readers;
using Common.WPF.ViewModels;
using System.IO;
using System.Windows.Media;

namespace RomForge.Core.Models.CD;

public class CdConvertFileItem(string filePath) : FileItemBase(filePath), Common.WPF.ViewModels.IConvertible
{
    public CdSourceFormat SourceFormat => Extension switch
    {
        "mds" => CdSourceFormat.MdfMds,
        "ccd" => CdSourceFormat.CcdImgSub,
        _ => CdSourceFormat.Unknown
    };

    private int _trackCount;

    public int TrackCount
    {
        get => _trackCount;
        set
        {
            if (SetProperty(ref _trackCount, value))
            {
                OnPropertyChanged(nameof(IsMultiTrack));
                OnPropertyChanged(nameof(AvailableOutputFormats));
                OnPropertyChanged(nameof(AvailableFormats));

                if (IsMultiTrack && OutputFormat == CdOutputFormat.Iso)
                    OutputFormat = CdOutputFormat.BinCue;
            }
        }
    }

    public bool IsMultiTrack => TrackCount > 1;

    public IReadOnlyList<CdOutputFormat> AvailableOutputFormats => IsMultiTrack
        ? [CdOutputFormat.BinCue]
        : [CdOutputFormat.BinCue, CdOutputFormat.Iso];

    private CdOutputFormat _outputFormat = CdOutputFormat.BinCue;

    public CdOutputFormat OutputFormat
    {
        get => _outputFormat;
        set
        {
            if (value == CdOutputFormat.Iso && IsMultiTrack)
                value = CdOutputFormat.BinCue;

            if (SetProperty(ref _outputFormat, value))
            {
                OnPropertyChanged(nameof(ExtensionLabel));
                OnPropertyChanged(nameof(SelectedTargetFormat));
            }
        }
    }

    public string ExtensionLabel => SourceFormat switch
    {
        CdSourceFormat.MdfMds or CdSourceFormat.CcdImgSub => $"{Extension}→{(_wantsChd ? "chd" : OutputFormat == CdOutputFormat.Iso ? "iso" : "cue")}",
        _ => Extension
    };

    public List<string> AvailableFormats => Extension == "ccd"
        ? [.. AvailableOutputFormats.Select(f => f == CdOutputFormat.Iso ? "ISO" : "CUE"), "CHD"]
        : [.. AvailableOutputFormats.Select(f => f == CdOutputFormat.Iso ? "ISO" : "CUE")];

    private bool _wantsChd;

    public string SelectedTargetFormat
    {
        get => _wantsChd ? "CHD" : OutputFormat == CdOutputFormat.Iso ? "ISO" : "CUE";
        set
        {
            _wantsChd = value == "CHD";

            if (!_wantsChd)
                OutputFormat = value == "ISO" ? CdOutputFormat.Iso : CdOutputFormat.BinCue;

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

    protected override string FormatSize(long bytes) => PickPack.Disk.ETC.FileSize.FormatSize(bytes);

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