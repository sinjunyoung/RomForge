namespace WiiU.WUP.Services
{
    public class AppXMLInfo
    {
        public AppXMLInfo() { }

        public int Version { get; set; } = 0;

        public long OSVersion { get; set; } = 0x0L;

        public long TitleID { get; set; } = 0x0L;

        public short TitleVersion { get; set; } = 0;

        public int SDKVersion { get; set; } = 0;

        public int AppType { get; set; } = 0x0;

        public short GroupID { get; set; } = 0;

        public byte[] OSMask { get; set; } = new byte[32];

        public long CommonId { get; set; } = 0x0L;
    }
}