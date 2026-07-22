using WiiU.Core.WUP.NusPackage.Interfaces;
using WiiU.Core.WUP.Services;

namespace WiiU.Core.WUP.Models
{
    public class FSTEntries : IHasData
    {
        private List<FSTEntry> entries = [];

        public FSTEntries()
        {
            FSTEntry root = new (true);
            entries.Add(root);
        }

        public List<FSTEntry> GetEntries()
        {
            return entries ??= [];
        }

        public bool IsEmtpy() => entries.Count == 0;

        public bool AddEntry(FSTEntry entry)
        {
            if (!entry.IsDir())
                return false;

            GetEntries().Add(entry);

            return true;
        }

        public void Update()
        {
            foreach (FSTEntry entry in GetEntries())
                entry.Update();

            UpdateDirRefs();
        }

        public List<FSTEntry> GetFSTEntriesByContent(Content content)
        {
            List<FSTEntry> result = [];

            foreach (FSTEntry curEntry in GetEntries())
                if (!curEntry.IsNotInPackage())
                    result.AddRange(curEntry.GetFSTEntriesByContent(content));

            return result;
        }

        public int GetFSTEntryCount()
        {
            int count = 0;

            foreach (FSTEntry entry in GetEntries())
                count += entry.GetEntryCount();

            return count;
        }

        public byte[] GetAsData()
        {
            JByteBuffer buffer = JByteBuffer.Allocate(GetDataSize());

            foreach (FSTEntry entry in GetEntries())
                buffer.Put(entry.GetAsData());

            return buffer.Array();
        }

        public int GetDataSize()
        {
            return GetFSTEntryCount() * 0x10;
        }

        public FSTEntry? GetRootEntry()
        {
            List<FSTEntry> entries_ = GetEntries();

            if (entries_.Count == 0)
                return null;

            return entries_[0];
        }

        public void UpdateDirRefs()
        {
            List<FSTEntry> entries_ = GetEntries();

            if (entries_.Count == 0) 
                return;

            FSTEntry root = entries_[0];

            root.SetParentOffset(0);
            root.SetNextOffset(FST.CurEntryOffset);

            FSTEntry? lastdir = root.UpdateDirRefs();

            lastdir?.SetNextOffset(FST.CurEntryOffset);
        }
    }
}