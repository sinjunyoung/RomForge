namespace RomForge.Core.Interfaces;

public interface IFileMetadata
{
    string FilePath { get; }

    string FileName { get; }

    string Extension { get; }

    string Directory { get; }

    string FileSize { get; }

    long FileSizeBytes { get; }
}