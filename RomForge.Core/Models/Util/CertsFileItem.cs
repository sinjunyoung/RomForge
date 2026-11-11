using Common.Localization;

using RomForge.Core.ViewModels;

namespace RomForge.Core.Models.Util;

public class CertsFileItem(string filePath) : FileItemBase(filePath, ItemStatus.Waiting)
{
    protected override string FormatSize(long bytes) => Common.Services.FileSize.FormatSize(bytes);
}