namespace WiiU.Core.WUP.Models
{
    public class ContentDetails
    {
        private bool isContent = true;

        private bool isEncrypted = true;

        private bool isHashed = false;

        private short groupID = 0x0000;
        private long parentTitleID = 0x0;

        private short entriesFlag = 0x0000;

        public ContentDetails(bool isHashed, short groupID, long parentTitleID, short entriesFlags)
        {
            SetHashed(isHashed);
            SetGroupID(groupID);
            SetParentTitleID(parentTitleID);
            SetEntriesFlag(entriesFlags);
        }

        public bool IsHashed() => isHashed;

        public void SetHashed(bool isHashed)
        {
            this.isHashed = isHashed;
        }

        public short GetGroupID() => groupID;

        public void SetGroupID(short groupID)
        {
            this.groupID = groupID;
        }

        public long GetParentTitleID() => parentTitleID;

        public void SetParentTitleID(long parentTitleID)
        {
            this.parentTitleID = parentTitleID;
        }

        public bool IsContent() => isContent;

        public void SetContent(bool isContent)
        {
            this.isContent = isContent;
        }

        public bool IsEncrypted() => isEncrypted;

        public void SetEncrypted(bool isEncrypted)
        {
            this.isEncrypted = isEncrypted;
        }

        public short GetEntriesFlag() => entriesFlag;

        public void SetEntriesFlag(short entriesFlag)
        {
            this.entriesFlag = entriesFlag;
        }

        public override string ToString()
        {
            return "ContentDetails [isContent=" + isContent + ", isEncrypted=" + isEncrypted + ", isHashed=" + isHashed + ", groupID=" + groupID + ", parentTitleID=" + parentTitleID + ", entriesFlag=" + entriesFlag + "]";
        }
    }
}