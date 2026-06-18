using System.IO;

namespace RomForge.Core.Models;

public class PatchEntry
{
    public string DisplayName { get; init; } = string.Empty;
    public string? ZipPath { get; init; }
    public string EntryPath { get; init; } = string.Empty;
    public bool IsZipEntry => ZipPath is not null;

    public string FileNameWithoutExtension =>
        Path.GetFileNameWithoutExtension(DisplayName);
}