using RomForge.Core.ViewModels;
using System.IO;

namespace RomForge.Core.Models.Util;

public class HashFileItem(string filePath) : FileItemBase(filePath)
{

    public string RawHash { get; set; } = string.Empty;

    public string HashResult
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                OnPropertyChanged(nameof(IsHashAvailable));
        }
    } = string.Empty;

    public bool IsCopied { get; set => SetProperty(ref field, value); }

    public bool IsHashAvailable => !string.IsNullOrEmpty(HashResult);

    public override string FileName => Path.GetFileName(FilePath);

    protected override string FormatSize(long bytes) => PickPack.Disk.Helpers.FileSize.FormatSize(bytes);
}