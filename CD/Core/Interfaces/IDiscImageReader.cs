using CD.Core.Models;

namespace CD.Core.Interfaces;

public interface IDiscImageReader
{
    bool CanRead(string filePath);

    DiscImage Read(string filePath);
}