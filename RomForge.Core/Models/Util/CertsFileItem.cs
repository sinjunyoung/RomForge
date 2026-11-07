using RomForge.Core.ViewModels;

namespace RomForge.Core.Models.Util;

public class CertsFileItem(string filePath) : FileItemBase(filePath, "대기 중")
{
    protected override string FormatSize(long bytes) => Common.Services.FileSize.FormatSize(bytes);
}