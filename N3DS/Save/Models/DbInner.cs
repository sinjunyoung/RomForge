using N3DS.Save;

namespace N3DS.Save.Models;

public class DbInner(Diff diff, Fat fat, FsMeta fs, int blockLen, int blockCount)
{
    public readonly Diff Diff = diff;

    public readonly Fat Fat = fat;

    public readonly FsMeta Fs = fs;

    public readonly int BlockLen = blockLen;

    public readonly int BlockCount = blockCount;
}