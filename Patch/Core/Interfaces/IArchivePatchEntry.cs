namespace Patch.Core.Interfaces;

public interface IArchivePatchEntry
{
    string FullPath { get; }

    long Length { get; }

    Stream Open();
}