using System.Runtime.CompilerServices;

namespace Patch.Core.Formats.Xdelta.Models;

internal static class LzmaConstants
{
    public const int kNumStates = 12;
    public const int kNumPosSlotBits = 6;
    public const int kDicLogSizeMin = 0;
    public const int kNumLenToPosStates = 4;
    public const int kMatchMinLen = 2;
    public const int kMatchMaxLen = 273;

    public const int kNumAlignBits = 4;
    public const int kAlignTableSize = 1 << kNumAlignBits;
    public const uint kAlignMask = kAlignTableSize - 1;

    public const int kStartPosModelIndex = 4;
    public const int kEndPosModelIndex = 14;
    public const int kNumFullDistances = 1 << (kEndPosModelIndex >> 1);

    public const int kNumPosStatesBitsMax = 4;
    public const int kNumPosStatesMax = 1 << kNumPosStatesBitsMax;

    public const int kNumLitContextBitsMax = 8;
    public const int kNumLitPosStatesBitsMax = 4;

    public const int kNumLowLenBits = 3;
    public const int kNumMidLenBits = 3;
    public const int kNumHighLenBits = 8;
    public const int kNumLowLenSymbols = 1 << kNumLowLenBits;
    public const int kNumMidLenSymbols = 1 << kNumMidLenBits;
    public const int kNumHighLenSymbols = 1 << kNumHighLenBits;
    public const int kNumLenSymbols = kNumLowLenSymbols + kNumMidLenSymbols + kNumHighLenSymbols;

    public const int kLenCoderSize = 2 + kNumPosStatesMax * (1 << kNumLowLenBits) + kNumPosStatesMax * (1 << kNumMidLenBits) + (1 << kNumHighLenBits);

    public const int kNumPosSlots = 1 << kNumPosSlotBits;
    public const int kPosSlotCoderSize = kNumLenToPosStates * kNumPosSlots;

    public const int kLitSubcoderSize = 0x300;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetLenToPosState(int len)
    {
        len -= kMatchMinLen;

        return len < kNumLenToPosStates ? len : kNumLenToPosStates - 1;
    }

    public static bool DecodeProperties(byte propsByte, out int lc, out int lp, out int pb)
    {
        if (propsByte >= 9 * 5 * 5)
        {
            lc = lp = pb = 0;

            return false;
        }

        lc = propsByte % 9;

        int remainder = propsByte / 9;

        lp = remainder % 5;
        pb = remainder / 5;

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte EncodeProperties(int lc, int lp, int pb) => (byte)(lc + 9 * (lp + 5 * pb));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int StateUpdateLiteral(int state)
    {
        if (state < 4) 
            return 0;

        if (state < 10) 
            return state - 3;

        return state - 6;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int StateUpdateMatch(int state) => state < 7 ? 7 : 10;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int StateUpdateLongRep(int state) => state < 7 ? 8 : 11;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int StateUpdateShortRep(int state) => state < 7 ? 9 : 11;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool StateIsLiteral(int state) => state < 7;
}