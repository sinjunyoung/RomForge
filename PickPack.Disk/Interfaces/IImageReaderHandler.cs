namespace PickPack.Disk.Interfaces;

public interface IImageReaderHandler
{
    Task WriteImageAsync(FileStream sourceStream, string outputPath, long totalSize, CancellationToken ct);
}