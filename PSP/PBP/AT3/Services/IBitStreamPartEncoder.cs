using PSP.PBP.AT3.Enums;
using PSP.PBP.AT3.IO;
using PSP.PBP.AT3.Models;

namespace PSP.PBP.AT3.Services;

internal interface IBitStreamPartEncoder
{
    PartStatus Encode(EncodeCtx ctx, BitAllocHandler ba);

    void Dump(BitStreamWriter bs);

    void Reset();
}