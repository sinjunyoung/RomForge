using Patch.Core.Formats.Xdelta.Models;

namespace Patch.Core.Formats.Xdelta;

public static class Xd3CodeTable
{
    private const int NearModes = 4;
    private const int SameModes = 3;
    private const int AddSizes = 17;
    private const int CpySizes = 15;
    private const int AddCopyAddMax = 4;
    private const int AddCopyNearCpyMax = 6;
    private const int AddCopySameCpyMax = 4;
    private const int CopyAddAddMax = 1;
    private const int CopyAddNearCpyMax = 4;
    private const int CopyAddSameCpyMax = 4;

    public static readonly Xd3Dinst[] Rfc3284 = Build();

    private static Xd3Dinst[] Build()
    {
        var tbl = new Xd3Dinst[256];
        int d = 0;
        int cpyModes = 2 + NearModes + SameModes;

        tbl[d++].Type1 = Xd3Constants.Xd3Run;
        tbl[d++].Type1 = Xd3Constants.Xd3Add;

        for (int size1 = 1; size1 <= AddSizes; size1++, d++)
        {
            tbl[d].Type1 = Xd3Constants.Xd3Add;
            tbl[d].Size1 = (byte)size1;
        }

        for (int mode = 0; mode < cpyModes; mode++)
        {
            tbl[d++].Type1 = (byte)(Xd3Constants.Xd3Cpy + mode);

            for (int size1 = Xd3Constants.MinMatch; size1 < Xd3Constants.MinMatch + CpySizes; size1++, d++)
            {
                tbl[d].Type1 = (byte)(Xd3Constants.Xd3Cpy + mode);
                tbl[d].Size1 = (byte)size1;
            }
        }

        for (int mode = 0; mode < cpyModes; mode++)
        {
            for (int size1 = 1; size1 <= AddCopyAddMax; size1++)
            {
                int max = mode < 2 + NearModes ? AddCopyNearCpyMax : AddCopySameCpyMax;

                for (int size2 = Xd3Constants.MinMatch; size2 <= max; size2++, d++)
                {
                    tbl[d].Type1 = Xd3Constants.Xd3Add;
                    tbl[d].Size1 = (byte)size1;
                    tbl[d].Type2 = (byte)(Xd3Constants.Xd3Cpy + mode);
                    tbl[d].Size2 = (byte)size2;
                }
            }
        }

        for (int mode = 0; mode < cpyModes; mode++)
        {
            int max = mode < 2 + NearModes ? CopyAddNearCpyMax : CopyAddSameCpyMax;

            for (int size1 = Xd3Constants.MinMatch; size1 <= max; size1++)
            {
                for (int size2 = 1; size2 <= CopyAddAddMax; size2++, d++)
                {
                    tbl[d].Type1 = (byte)(Xd3Constants.Xd3Cpy + mode);
                    tbl[d].Size1 = (byte)size1;
                    tbl[d].Type2 = Xd3Constants.Xd3Add;
                    tbl[d].Size2 = (byte)size2;
                }
            }
        }

        if (d != 256)
            throw new Xd3Exception("internal: rfc3284 code table build produced wrong entry count");

        return tbl;
    }
}