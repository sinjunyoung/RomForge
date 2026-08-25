using Patch.Core.Formats.Xdelta.Models;

namespace Patch.Core.Formats.Xdelta.Services;

public interface IXd3BlockSource
{
    void FillBlock(Xd3Source source, long blockNumber);
}