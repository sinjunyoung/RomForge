using RomForge.Core.Interfaces;
using System.IO;

namespace RomForge.Core.ViewModels;

public abstract class FileItemBase : ProcessableItemBase, IFileMetadata
{
    private readonly Lazy<long> _fileSizeBytes;

    public string FilePath { get; }

    public virtual string FileName => Path.GetFileNameWithoutExtension(FilePath);

    public virtual string Extension => Path.GetExtension(FilePath).TrimStart('.').ToLowerInvariant();

    public string Directory => Path.GetDirectoryName(FilePath) ?? string.Empty;

    public string FileSize => FormatSize(FileSizeBytes);

    public virtual long FileSizeBytes => _fileSizeBytes.Value;

    protected FileItemBase(string filePath, string initialStatus = "") : base(initialStatus)
    {
        FilePath = filePath;
        _fileSizeBytes = new Lazy<long>(() => CalculateSize(filePath));
    }

    protected virtual long CalculateSize(string filePath)
    {
        var info = new FileInfo(filePath);

        return info.Exists ? info.Length : 0;
    }

    protected abstract string FormatSize(long bytes);
}