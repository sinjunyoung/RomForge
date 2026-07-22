using System.Text;

namespace WiiU.Core.WUP.Services
{
    public class AppXMLInfo
    {
        private int version = 0;
        private long osVersion = 0x0L;
        private long titleID = 0x0L;
        private short titleVersion = 0;
        private int sdkVersion = 0;
        private int appType = 0x0;
        private short groupID = 0;
        private byte[] osMask = new byte[32];
        private long common_id = 0x0L;

        public AppXMLInfo()
        {
        }

        public int GetVersion() => version;

        public void SetVersion(int version) => this.version = version;

        public long GetOSVersion() => osVersion;

        public void SetOSVersion(long osVersion) => this.osVersion = osVersion;

        public long GetTitleID() => titleID;

        public void SetTitleID(long titleID) => this.titleID = titleID;

        public short GetTitleVersion() => titleVersion;

        public void SetTitleVersion(short titleVersion) => this.titleVersion = titleVersion;

        public int GetSDKVersion() => sdkVersion;

        public void SetSDKVersion(int sdkVersion) => this.sdkVersion = sdkVersion;

        public int GetAppType() => appType;

        public void SetAppType(int appType) => this.appType = appType;

        public short GetGroupID() => groupID;

        public void SetGroupID(short groupID) => this.groupID = groupID;

        public byte[] GetOSMask() => osMask;

        public void SetOSMask(byte[] osMask) => this.osMask = osMask;

        public long GetCommon_id() => common_id;

        public void SetCommon_id(long common_id) => this.common_id = common_id;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append('[');

            foreach (var b in osMask) 
                sb.Append(b).Append(", ");

            if (osMask.Length > 0) 
                sb.Length -= 2;

            sb.Append(']');

            return "AppXMLInfo [version=" + version + ", OSVersion=" + osVersion + ", titleID=" + titleID
                + ", titleVersion=" + titleVersion + ", SDKVersion=" + sdkVersion + ", appType=" + appType
                + ", groupID=" + groupID + ", OSMask=" + sb + ", common_id=" + common_id + "]";
        }
    }
}