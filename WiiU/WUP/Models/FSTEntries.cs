using WiiU.WUP.Interfaces;
using WiiU.WUP.Services;

namespace WiiU.WUP.Models
{
    public class FSTEntries : IHasData
    {
        private readonly List<FSTEntry> _entries = [];

        public FSTEntries()
        {
            FSTEntry root = new (true);
            _entries.Add(root);
        }

        public bool IsEmtpy() => _entries.Count == 0;

        public bool AddEntry(FSTEntry entry)
        {
            if (!entry.IsDir)
                return false;

            _entries.Add(entry);

            return true;
        }

        public void Update()
        {
            foreach (FSTEntry entry in _entries)
                entry.Update();

            UpdateDirRefs();
        }

        public List<FSTEntry> GetFSTEntriesByContent(Content content)
        {
            List<FSTEntry> result = [];

            foreach (FSTEntry curEntry in _entries)
                if (!curEntry.IsNotInPackage)
                    result.AddRange(curEntry.GetFSTEntriesByContent(content));

            return result;
        }

        public int GetFSTEntryCount()
        {
            int count = 0;

            foreach (FSTEntry entry in _entries)
                count += entry.GetEntryCount();

            return count;
        }

        public byte[] GetAsData()
        {
            JByteBuffer buffer = JByteBuffer.Allocate(GetDataSize());

            foreach (FSTEntry entry in _entries)
                buffer.Put(entry.GetAsData());

            return buffer.Array();
        }

        public int GetDataSize() => GetFSTEntryCount() * 0x10;

        public FSTEntry? GetRootEntry()
        {
            if (_entries.Count == 0)
                return null;

            return _entries[0];
        }

        public void UpdateDirRefs()
        {
            List<FSTEntry> entries_ = _entries;

            if (entries_.Count == 0) 
                return;

            FSTEntry root = entries_[0];

            root.ParentOffset = 0;
            root.NextOffset = FST.CurEntryOffset;

            FSTEntry? lastdir = root.UpdateDirRefs();

            if(lastdir != null)
                lastdir.NextOffset = FST.CurEntryOffset;
        }
    }
}