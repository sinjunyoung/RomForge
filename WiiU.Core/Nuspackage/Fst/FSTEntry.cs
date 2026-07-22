using System;
using System.Collections.Generic;
using System.IO;
using NUSPacker.Nuspackage.Contents;
using NUSPacker.Nuspackage.Interfaces;
using NUSPacker.Utils;

namespace NUSPacker.Nuspackage.Fst
{
    /// <summary>
    /// Represents a Entry of a FST
    /// </summary>
    public class FSTEntry : IHasData, ICloneable
    {
        public enum Types : byte
        {
            DIR = 0x01,
            notInNUS = 0x80,
            WiiVC = 0x02,
        }

        public enum Flags : short
        {
            NOBIGFILE = 0x04,
            HASHED = 0x400,
        }

        /// <summary>Absolute path on disk backing this entry, or null if this entry is not a real file (e.g. root).</summary>
        private string? file;

        /// <summary>
        /// Attributes for all FSTEntries
        /// </summary>
        private string filename = "";
        private FSTEntry? parent = null;
        private List<FSTEntry>? children = null;
        private int nameOffset = 0;
        private int entryOffset = 0x00;

        private short flags;

        /// <summary>Attributes when FSTEntry is a DIR</summary>
        private bool isDirFlag = false;
        private int parentOffset = 0;
        private int nextOffset = 0;

        /// <summary>Attributes when FSTEntry is a file</summary>
        private long filesize = 0;
        private long fileoffset = 0;

        /// <summary>Attributes when FSTEntry is the root of the FST</summary>
        private bool isRootFlag = false;

        /// <summary>When this FSTEntry is the root, we need to know the total EntryCount of the FST.</summary>
        private int root_entryCount = 0;

        /// <summary>This FSTEntry belongs to content....</summary>
        private Content? content = null; // We need the ID

        /// <summary>SHA1 hash of the decrypted file padded to the next full 32kb (0x8000 bytes)</summary>
        private byte[]? decryptedSHA1 = new byte[0x14];

        private bool bigFile = false; // TODO: Check it...

        private bool hashedFile = false;

        private bool notInPackage = false;

        /// <summary>
        /// Lazily-opened stream source for this entry, when it was created via the stream-factory
        /// constructor. This is the preferred way to hand this library a file whose bytes live
        /// somewhere other than a real path on disk (e.g. inside an already-open WUD/WUX/WUA/WUP
        /// source) WITHOUT ever materializing the whole file into a single byte[] - important for
        /// content files that can be hundreds of MB to several GB.
        /// </summary>
        private Func<Stream>? streamFactory = null;

        public FSTEntry(string filePath) : this(filePath, false)
        {
        }

        public FSTEntry(string filePath, bool notInPackage)
        {
            bool isDirectory = Directory.Exists(filePath);
            bool isFileOnDisk = File.Exists(filePath);
            if (filePath == null || (!isDirectory && !isFileOnDisk))
            {
                throw new ArgumentException("Couldn't create FSTEntry, file is NULL or doesn't exist");
            }
            file = Path.GetFullPath(filePath);
            SetDir(isDirectory);
            SetFileName(isDirectory ? new DirectoryInfo(filePath).Name : Path.GetFileName(filePath));
            SetFileSize(isDirectory ? 0 : new FileInfo(filePath).Length);

            SetNotInPackage(notInPackage);

            if (IsFile())
            {
                decryptedSHA1 = null;
            }
        }

        /// <summary>
        /// Creates a file FSTEntry backed by a lazily-opened stream instead of a real file on disk.
        /// Preferred over pointing at a real path for anything that could be large, since the
        /// file's bytes are never fully materialized in memory at once - the packing pipeline only
        /// ever streams through them once, sequentially.
        /// </summary>
        /// <param name="name">The entry's file name (not a full path).</param>
        /// <param name="openRead">Called (once) to obtain a readable stream positioned at the start of the file's data. The caller is responsible for the stream being fresh/re-openable if this entry ends up read more than once.</param>
        /// <param name="length">The exact byte length of the data the stream will yield.</param>
        public FSTEntry(string name, Func<Stream> openRead, long length)
        {
            file = null;
            streamFactory = openRead ?? throw new ArgumentNullException(nameof(openRead));
            SetDir(false);
            SetFileName(name);
            SetFileSize(length);
            SetNotInPackage(false);
            decryptedSHA1 = null;
        }

        /// <summary>Stream factory for this entry if it was created via the stream-factory constructor, else null.</summary>
        public Func<Stream>? GetStreamFactory() => streamFactory;

        public FSTEntry(bool root)
        {
            file = null;
            if (root)
            {
                SetIsRoot(true);
                SetDir(true);
            }
        }

        // TODO: Make sure that the filename is unique for all children of this entry.
        public void AddChildren(FSTEntry fstEntry)
        {
            GetChildren().Add(fstEntry);
            fstEntry.SetParent(this);
        }

        public bool IsNotInPackage() => notInPackage;

        public void SetNotInPackage(bool notInPackage)
        {
            this.notInPackage = notInPackage;
        }

        /// <summary>Returns the parent of this FSTEntry</summary>
        public FSTEntry? GetParent() => parent;

        /// <summary>Sets the parent of this FSTEntry</summary>
        private void SetParent(FSTEntry child)
        {
            // TODO: check for null? if parent is a child/it self? idk. Need to think about it.
            parent = child;
        }

        /// <summary>Return the content this FSTEntry will be saved in.</summary>
        public Content? GetContent() => content;

        /// <summary>Sets a ref to content this file is a part of and sets the flags.</summary>
        public void SetContent(Content content)
        {
            SetFlags(content.GetEntriesFlags());
            this.content = content;
        }

        /// <summary>
        /// Sets a ref to content this file is a part of recursive.
        /// The same content will be set for ALL children.
        /// </summary>
        public void SetContentRecursive(Content content)
        {
            SetContent(content);
            foreach (FSTEntry entry in GetChildren())
            {
                entry.SetContentRecursive(content);
            }
        }

        /// <summary>Returns the path of the file backing this FSTEntry</summary>
        public string? GetFile() => file;

        /// <summary>Returns the name of this entry</summary>
        public string GetFilename() => filename;

        /// <summary>Sets the name of this entry</summary>
        public void SetFileName(string filename)
        {
            this.filename = filename;
        }

        /// <summary>Returns the filesize of this entry. 0 if this is not a file</summary>
        public long GetFilesize()
        {
            if (!IsFile()) return 0;
            return filesize;
        }

        /// <summary>Sets the new filesize</summary>
        public void SetFileSize(long filesize)
        {
            this.filesize = filesize;
        }

        /// <summary>Returns the file offset of this entry in the Content file. 0 if this not a file</summary>
        public long GetFileOffset() => fileoffset;

        /// <summary>Sets the new file offset</summary>
        public void SetFileOffset(long fileOffset)
        {
            fileoffset = fileOffset;
        }

        /// <summary>Sets this FSTEntry to root.</summary>
        private void SetIsRoot(bool isRoot)
        {
            isRootFlag = isRoot;
        }

        /// <summary>Returns true if this FSTEntry is the root of the FST</summary>
        public bool IsRoot() => isRootFlag;

        /// <summary>Sets if the directory is a entry</summary>
        public void SetDir(bool isDir)
        {
            isDirFlag = isDir;
        }

        /// <summary>Returns if this entry is a dir</summary>
        public bool IsDir() => isDirFlag;

        /// <summary>Returns if this entry is a file</summary>
        public bool IsFile() => !(IsDir() || IsNotInPackage());

        public bool IsBigFile() => bigFile;

        public void SetBigFile(bool bigFile)
        {
            this.bigFile = bigFile;
        }

        public bool IsHashedFile() => hashedFile;

        public void SetHashedFile(bool hashedFile)
        {
            this.hashedFile = hashedFile;
        }

        /// <summary>Returns the type of this FSTEntry</summary>
        public byte GetEntryType()
        {
            byte type = 0;
            if (IsDir()) type |= (byte)Types.DIR;
            if (IsNotInPackage()) type |= (byte)Types.notInNUS;
            if (GetFilename().EndsWith("nfs")) type |= (byte)Types.WiiVC;
            return type;
        }

        /// <summary>Returns the flags of this FSTEntry</summary>
        public short GetFlags() => flags;

        /// <summary>
        /// Returns the child of this FSTEntry that has the given name.
        /// Result is null if the FSTEntry doesn't contain a child with the given name.
        /// </summary>
        public FSTEntry? GetEntryByName(string name)
        {
            foreach (FSTEntry f in GetChildren())
            {
                if (f.GetFilename().Equals(name))
                {
                    return f;
                }
            }
            return null;
        }

        public byte[] GetAsData()
        {
            JByteBuffer buffer = JByteBuffer.Allocate(GetDataSize());
            if (IsRoot())
            {
                buffer.Put((byte)1);
                buffer.Put(new byte[0x07]);
                buffer.PutInt(root_entryCount);
                buffer.Put(new byte[0x04]);
            }
            else
            {
                buffer.Put(GetEntryType());
                buffer.Put((byte)((nameOffset >> 16) & 0xFF)); // We need to write a 24bit int..
                buffer.PutShort((short)(nameOffset & 0xFFFF));
                if (IsDir())
                {
                    buffer.PutInt(parentOffset);
                    buffer.PutInt(nextOffset);
                }
                else if (IsFile())
                {
                    buffer.PutInt((int)(fileoffset >> 5));
                    buffer.PutInt((int)filesize);
                }
                else if (IsNotInPackage())
                {
                    buffer.PutInt(0);
                    buffer.PutInt((int)filesize);
                }
                buffer.PutShort(GetFlags());
                buffer.PutShort((short)content!.GetID());
            }

            // Let's call this recursive
            if (children != null)
            {
                foreach (FSTEntry entry in GetChildren())
                {
                    buffer.Put(entry.GetAsData());
                }
            }
            return buffer.Array();
        }

        public int GetDataSize()
        {
            int size = 0x10;
            foreach (FSTEntry entry in GetChildren())
            {
                size += entry.GetDataSize();
            }
            return size;
        }

        /// <summary>Returns the SHA1 hash of the decrypted data filled up to the next 32KB (0x8000 bytes)</summary>
        public byte[] GetDecryptedHash()
        {
            if (decryptedSHA1 == null) // Calculate this only when we really need it...
            {
                CalculateDecryptedHash();
            }
            return decryptedSHA1!;
        }

        public void SetNameOffset(int offset)
        {
            if (offset > 0xFFFFFF)
            {
                Console.WriteLine("Warning, filename offset is too big. Maximum is " + 0xFFFFFF + " tried to set to" + offset);
            }
            nameOffset = offset;
        }

        /// <summary>
        /// Updates the entryOffset, name section etc.
        /// Dir connections are not updated
        /// </summary>
        public void Update()
        {
            // Adds the current filename to the string section
            SetNameOffset(FST.GetStringPos());
            FST.AddString(filename);
            SetEntryOffset(FST.curEntryOffset);
            FST.curEntryOffset++;

            // TODO: check if obsolete (should be as the UpdateDirRefs is implemented) it should do the same.
            if (IsDir() && !IsRoot())
            {
                SetParentOffset(GetParent()!.GetEntryOffset());
            }

            if (GetContent() != null && IsFile())
            {
                long fileoffset_ = GetContent()!.GetOffsetForFileAndIncrease(this);
                SetFileOffset(fileoffset_);
            }

            // Update recursive!!!
            foreach (FSTEntry entry in GetChildren())
            {
                entry.Update();
            }
        }

        /// <summary>
        /// Updates the directory refs.
        /// Returns a FSTEntry when the directory has no files. The result need have the NextOffset set to the next dir.
        /// </summary>
        public FSTEntry? UpdateDirRefs()
        {
            if (!(IsDir() || IsRoot())) return null;
            if (parent != null)
            {
                SetParentOffset(GetParent()!.GetEntryOffset());
            }

            FSTEntry? result = null;

            var dirChildren = GetDirChildren();
            for (int i = 0; i < dirChildren.Count; i++)
            {
                FSTEntry cur_dir = dirChildren[i];

                if (i + 1 < dirChildren.Count)
                {
                    cur_dir.SetNextOffset(dirChildren[i + 1].entryOffset);
                }

                FSTEntry? cur_result = cur_dir.UpdateDirRefs();

                if (cur_result != null)
                {
                    FSTEntry cur_foo = cur_result.GetParent()!;
                    while (cur_foo.GetNextOffset() == 0)
                    {
                        cur_foo = cur_foo.GetParent()!;
                    }
                    cur_result.SetNextOffset(cur_foo.GetNextOffset());
                }

                if (!(i + 1 < dirChildren.Count))
                {
                    result = cur_dir;
                }
            }
            return result;
        }

        private int GetNextOffset() => nextOffset;

        /// <summary>
        /// Sets the entryoffset of this FSTEntry in the FST.
        /// Offset in terms of entry number X.
        /// </summary>
        public void SetEntryOffset(int entryOffset)
        {
            this.entryOffset = entryOffset;
        }

        /// <summary>
        /// Returns the entryoffset of this FSTEntry in the FST.
        /// Offset in terms of entry number X.
        /// </summary>
        public int GetEntryOffset() => entryOffset;

        public void SetNextOffset(int nextOffset)
        {
            this.nextOffset = nextOffset;
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();

            if (IsDir()) sb.Append("DIR: ").Append('\n');
            if (IsDir()) sb.Append("Filename: ").Append(GetFilename()).Append('\n');
            if (IsDir()) sb.Append("       ID:").Append(GetEntryOffset()).Append('\n');
            if (IsDir()) sb.Append(" ParentID:").Append(parentOffset).Append('\n');
            if (IsDir()) sb.Append("   NextID:").Append(nextOffset).Append('\n');
            foreach (FSTEntry e in GetChildren())
            {
                sb.Append(e.ToString());
            }

            return sb.ToString();
        }

        public void PrintRecursive(int space)
        {
            for (int i = 0; i < space; i++)
            {
                Console.Write(" ");
            }
            Console.Write(GetFilename());
            if (IsNotInPackage())
            {
                Console.Write(" (not in package)");
            }
            Console.WriteLine();
            foreach (FSTEntry child in GetDirChildren(true))
            {
                child.PrintRecursive(space + 1);
            }
            foreach (FSTEntry child in GetFileChildren(true))
            {
                child.PrintRecursive(space + 1);
            }
        }

        public List<FSTEntry> GetFSTEntriesByContent(Content content)
        {
            List<FSTEntry> entries = new List<FSTEntry>();
            if (this.content == null)
            {
                if (isDirFlag)
                {
                    Console.Error.WriteLine("The folder \"" + GetFilename() + "\" is emtpy. Please add a dummy file to it.");
                }
                else
                {
                    Console.Error.WriteLine("The file \"" + GetFilename() + "\" is not assigned to any content (.app).");
                    Console.Error.WriteLine("Please delete it or write a corresponding content rule");
                }
                Environment.Exit(0);
            }
            else
            {
                if (this.content.Equals(content))
                {
                    entries.Add(this);
                }
            }
            foreach (FSTEntry child in GetChildren())
            {
                entries.AddRange(child.GetFSTEntriesByContent(content));
            }
            return entries;
        }

        public List<FSTEntry> GetChildren()
        {
            return children ??= new List<FSTEntry>();
        }

        public int GetEntryCount()
        {
            int count = 1;
            foreach (FSTEntry entry in GetChildren())
            {
                count += entry.GetEntryCount();
            }
            return count;
        }

        public void SetParentOffset(int i)
        {
            parentOffset = i;
        }

        public void SetEntryCount(int fstEntryCount)
        {
            root_entryCount = fstEntryCount;
        }

        /// <summary>
        /// Returns all children that are directories. Returns an empty list if the FSTEntry is not a directory or doesn't have directories as children
        /// </summary>
        public List<FSTEntry> GetDirChildren() => GetDirChildren(false);

        public List<FSTEntry> GetDirChildren(bool all)
        {
            List<FSTEntry> result = new List<FSTEntry>();
            foreach (FSTEntry child in GetChildren())
            {
                if (child.IsDir() && (all || !child.IsNotInPackage()))
                {
                    result.Add(child);
                }
            }
            return result;
        }

        /// <summary>
        /// Returns all children that are files. Returns an empty list if the FSTEntry is not a directory or doesn't have files as children
        /// </summary>
        public List<FSTEntry> GetFileChildren() => GetFileChildren(false);

        public List<FSTEntry> GetFileChildren(bool all)
        {
            List<FSTEntry> result = new List<FSTEntry>();
            foreach (FSTEntry child in GetChildren())
            {
                if (child.IsFile() || (all && !child.IsDir()))
                {
                    result.Add(child);
                }
            }
            return result;
        }

        public void CalculateDecryptedHash()
        {
            decryptedSHA1 = HashUtil.HashSHA1(file!, 0x8000);
        }

        public void SetFlags(short flags)
        {
            this.flags = flags;
        }

        public FSTEntry Clone()
        {
            return (FSTEntry)MemberwiseClone();
        }

        object ICloneable.Clone() => Clone();
    }
}
