using System;
using System.IO;
using NUSPacker.Nuspackage.Contents;
using NUSPacker.Nuspackage.Crypto;
using NUSPacker.Utils;

namespace NUSPacker.Nuspackage.Packaging
{
    public class NUSPackage
    {
        private Ticket ticket = null!;
        private TMD tmd = null!;
        private FST fst = null!;

        private string outputdir = "output";

        public Ticket GetTicket() => ticket;

        public void SetTicket(Ticket ticket)
        {
            this.ticket = ticket;
        }

        public string GetOutputdir() => outputdir;

        public void SetOutputdir(string outputdir)
        {
            this.outputdir = outputdir;
        }

        public TMD GetTMD() => tmd;

        public void SetTMD(TMD tmd)
        {
            this.tmd = tmd;
        }

        public FST GetFST() => fst;

        public void SetFST(FST fst)
        {
            this.fst = fst;
        }

        public Contents.Contents GetContents() => GetFST().GetContents();

        public ContentInfos GetContentInfos() => GetTMD().GetContentInfos();

        public void PackContents(string outputDir)
        {
            PackContents(outputDir, null);
        }

        public void PackContents(string outputDir, System.Action<Content>? onContentPacked)
        {
            PackContents(outputDir, onContentPacked, null);
        }

        public void PackContents(string outputDir, System.Action<Content>? onContentPacked, System.Action<Content, string, long, long>? onContentBytesProcessed)
        {
            if (outputDir != null && outputDir.Length != 0)
            {
                SetOutputdir(outputDir);
            }
            Console.WriteLine("Packing contents.");
            // Do this before creating the title.tmd.
            try
            {
                GetFST().GetContents().PackContents(outputDir!, onContentPacked, onContentBytesProcessed);
            }
            catch (IOException e1)
            {
                Console.Error.WriteLine(e1);
            }

            // Set the correct FST hash and size.
            Content fstContent = GetContents().GetFSTContent();
            fstContent.SetHash(HashUtil.HashSHA1(GetFST().GetAsData()));
            fstContent.SetEncryptedFileSize(GetFST().GetAsData().Length);

            // Update the grouphash
            ContentInfo contentInfo = GetContentInfos().GetContentInfo(0);
            contentInfo.SetSHA2Hash(HashUtil.HashSHA2(GetContents().GetAsData()));
            // And the tmd contentinfo hash
            GetTMD().UpdateContentInfoHash();

            try
            {
                using (FileStream fos = new FileStream(GetOutputdir() + "/title.tmd", FileMode.Create, FileAccess.Write))
                {
                    byte[] data = tmd.GetAsData();
                    fos.Write(data, 0, data.Length);
                }
                Console.WriteLine("TMD saved to    " + GetOutputdir() + "/title.tmd");

                using (FileStream fos = new FileStream(GetOutputdir() + "/title.cert", FileMode.Create, FileAccess.Write))
                {
                    byte[] data = Cert.GetCertAsData();
                    fos.Write(data, 0, data.Length);
                }
                Console.WriteLine("Cert saved to   " + GetOutputdir() + "/title.cert");

                using (FileStream fos = new FileStream(GetOutputdir() + "/title.tik", FileMode.Create, FileAccess.Write))
                {
                    byte[] data = ticket.GetAsData();
                    fos.Write(data, 0, data.Length);
                }
                Console.WriteLine("Ticket saved to " + GetOutputdir() + "/title.tik");
                Console.WriteLine();
            }
            catch (IOException e)
            {
                Console.Error.WriteLine(e);
            }
        }

        public void PrintTicketInfos()
        {
            Console.WriteLine("Encrypted with this key           : " + GetTicket().GetDecryptedKey());
            Console.WriteLine("Key encrypted with this key       : " + GetTicket().GetEncryptWith());
            Console.WriteLine();
            Console.WriteLine("Encrypted key                     : " + GetTicket().GetEncryptedKey());
        }

        public Encryption GetEncryption() => GetTMD().GetEncryption();
    }
}
