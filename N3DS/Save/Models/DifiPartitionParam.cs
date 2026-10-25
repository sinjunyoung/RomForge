namespace N3DS.Save.Models;

public class DifiPartitionParam
{
    public int DpfsLevel2BlockLen = 0;

    public int DpfsLevel3BlockLen = 0;

    public int IvfcLevel1BlockLen = 0;

    public int IvfcLevel2BlockLen = 0;

    public int IvfcLevel3BlockLen = 0;

    public int IvfcLevel4BlockLen = 0;

    public int DataLen = 0;

    public bool ExternalIvfcLevel4 = false;

    public int GetAlign() => Math.Max(DpfsLevel2BlockLen, Math.Max(DpfsLevel3BlockLen, Math.Max(IvfcLevel1BlockLen, Math.Max(IvfcLevel2BlockLen, Math.Max(IvfcLevel3BlockLen, IvfcLevel4BlockLen)))));
}