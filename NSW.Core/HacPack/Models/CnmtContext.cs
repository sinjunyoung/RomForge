namespace NSW.HacPack.Models;

public class CnmtContext
{
    public CnmtHeader Header;

    public byte ContentRecordsCount;

    public CnmtContentRecord[] ContentRecords = new CnmtContentRecord[32];
}