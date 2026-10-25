using PBP.AT3.Enums;
using PBP.AT3.IO;
using PBP.AT3.Models;

namespace PBP.AT3.Services;

internal interface IBitStreamPartEncoder
{
    PartStatus Encode(EncodeCtx ctx, BitAllocHandler ba);

    void Dump(BitStreamWriter bs);

    void Reset();
}