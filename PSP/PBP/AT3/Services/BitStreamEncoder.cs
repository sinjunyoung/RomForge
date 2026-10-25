using PSP.PBP.AT3.Enums;
using PSP.PBP.AT3.IO;
using PSP.PBP.AT3.Models;

namespace PSP.PBP.AT3.Services;

internal sealed class BitStreamEncoder(List<IBitStreamPartEncoder> parts)
{
    private readonly BitAllocHandler _ba = new();

    public void Do(EncodeCtx ctx, BitStreamWriter bs)
    {
        bool cont = false;

        do
        {
            for (_ba.CurEncPos = _ba.RepeatEncPos; _ba.CurEncPos < parts.Count; _ba.CurEncPos++)
            {
                var status = parts[_ba.CurEncPos].Encode(ctx, _ba);

                if (_ba.NeedRepeat)
                {
                    _ba.NeedRepeat = false;
                    cont = true;

                    break;
                }

                cont = false;

                if (status == PartStatus.Repeat)
                {
                    cont = true;

                    for (int i = 0; i < _ba.CurEncPos; i++)
                        parts[i].Reset();

                    _ba.RepeatEncPos = 0;

                    break;
                }
            }
        } while (cont);

        foreach (var part in parts)
            part.Dump(bs);

        _ba.RepeatEncPos = 0;
    }
}