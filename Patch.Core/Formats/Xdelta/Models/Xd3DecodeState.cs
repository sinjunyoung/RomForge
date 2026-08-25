namespace Patch.Core.Formats.Xdelta.Models;

public enum Xd3DecodeState
{
    VcHead = 0,
    HdrInd = 1,
    SecondId = 2,
    TabLen = 3,
    Near = 4,
    Same = 5,
    TabDat = 6,
    AppLen = 7,
    AppDat = 8,
    WinInd = 9,
    CpyLen = 10,
    CpyOff = 11,
    EncLen = 12,
    TgtLen = 13,
    DelInd = 14,
    DataLen = 15,
    InstLen = 16,
    AddrLen = 17,
    Cksum = 18,
    Data = 19,
    Inst = 20,
    Addr = 21,
    Emit = 22,
    Finish = 23,
    Aborted = 24
}