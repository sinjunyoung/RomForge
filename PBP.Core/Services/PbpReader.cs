using PBP.Core.Enums;
using PBP.Core.Models;

namespace PBP.Core.Services;


public class PbpReader
{
    private const int HEADER_SFO_OFFSET = 0x08;
    private const int HEADER_ICON0_OFFSET = 0x0C;
    private const int HEADER_ICON1_OFFSET = 0x10;
    private const int HEADER_PIC0_OFFSET = 0x14;
    private const int HEADER_PIC1_OFFSET = 0x18;
    private const int HEADER_SND0_OFFSET = 0x1C;
    private const int HEADER_PSP_OFFSET = 0x20;
    private const int HEADER_PSAR_OFFSET = 0x24;

    public const int ISO_BLOCK_SIZE = 0x930;

    private const uint PBPMAGIC = 0x50425000;

    public SfoFile SFOData { get; private set; }
    public List<PbpDiscEntry> Discs { get; private set; }

    public PbpReader(Stream stream)
    {
        var buffer = new byte[16];
        stream.Read(buffer, 0, 4);

        if (BitConverter.ToUInt32(buffer, 0) != PBPMAGIC)
            throw new Exception("Invalid Header found while reading PBP");

        stream.Seek(HEADER_SFO_OFFSET, SeekOrigin.Begin);
        var sfoOffset = stream.ReadUInteger();

        stream.Seek(sfoOffset, SeekOrigin.Begin);
        SFOData = stream.ReadSFO(sfoOffset);

        stream.Seek(HEADER_PSAR_OFFSET, SeekOrigin.Begin);
        var psarOffset = stream.ReadInteger();

        if (psarOffset == 0)
            throw new Exception("Invalid PSAR offset or corrupted file");

        stream.Seek(psarOffset, SeekOrigin.Begin);
        stream.Read(buffer, 0, 12);
        var header = System.Text.Encoding.ASCII.GetString(buffer, 0, 12);

        if (header == "PSISOIMG0000")
        {
            Discs =
            [
                new PbpDiscEntry(stream, psarOffset, 1)
            ];
        }
        else
        {
            stream.Read(buffer, 12, 4);
            var header16 = System.Text.Encoding.ASCII.GetString(buffer, 0, 16);

            if (header16 != "PSTITLEIMG000000")
                throw new Exception("Invalid header");

            stream.ReadInteger();
            stream.ReadInteger();

            if (stream.ReadUInteger() != 0x2CC9C5BC) 
                throw new Exception("Invalid header");

            if (stream.ReadUInteger() != 0x33B5A90F) 
                throw new Exception("Invalid header");

            if (stream.ReadUInteger() != 0x06F6B4B3) 
                throw new Exception("Invalid header");

            if (stream.ReadUInteger() != 0xB25945BA) 
                throw new Exception("Invalid header");

            for (var i = 0; i < 0x76; i++)
                stream.ReadInteger();

            var isoPositions = new uint[5];
            stream.Read(isoPositions, 5);

            Discs = [.. isoPositions
                .Where(x => x > 0)
                .Select((x, i) => new PbpDiscEntry(stream, psarOffset + (int)x, i + 1))];

            //if (Discs.Any(d => d.IsPvdMismatch))
            //    throw new Exception("손상되거나 변형된 게임 파일입니다.");
        }
    }

    public static int Seek(ResourceType resource, Stream stream)
    {
        int offset = resource switch
        {
            ResourceType.SFO => HEADER_SFO_OFFSET,
            ResourceType.ICON0 => HEADER_ICON0_OFFSET,
            ResourceType.ICON1 => HEADER_ICON1_OFFSET,
            ResourceType.PIC0 => HEADER_PIC0_OFFSET,
            ResourceType.PIC1 => HEADER_PIC1_OFFSET,
            ResourceType.SND0 => HEADER_SND0_OFFSET,
            ResourceType.PSP => HEADER_PSP_OFFSET,
            ResourceType.PSAR => HEADER_PSAR_OFFSET,
            _ => throw new ArgumentOutOfRangeException(nameof(resource))
        };

        stream.Seek(offset, SeekOrigin.Begin);
        var start = stream.ReadInteger();
        var end = resource != ResourceType.PSAR
            ? stream.ReadInteger()
            : (int)stream.Length;

        stream.Seek(start, SeekOrigin.Begin);

        return end - start;
    }

    public static bool TryGetResourceStream(ResourceType resource, Stream stream, out Stream? outputStream)
    {
        var length = Seek(resource, stream);

        if (length > 0)
        {
            var buffer = new byte[length];
            stream.Read(buffer, 0, length);
            outputStream = new MemoryStream(buffer);

            return true;
        }
        outputStream = null;

        return false;
    }
}