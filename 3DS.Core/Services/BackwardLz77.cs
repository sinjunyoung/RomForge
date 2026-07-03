namespace _3DS.Core.Services;

public static class BackwardLz77
{
    private const int FooterSize = 8;

    public static byte[] Decompress(byte[] compressed)
    {
        int compressedSize = compressed.Length;

        if (compressedSize < FooterSize)
            throw new InvalidDataException("압축된 code 데이터가 너무 작아 BackwardLZ77 푸터를 읽을 수 없습니다.");

        uint bufferTopAndBottom = BitConverter.ToUInt32(compressed, compressedSize - 8);
        uint originalBottom = BitConverter.ToUInt32(compressed, compressedSize - 4);

        uint top = bufferTopAndBottom & 0xFFFFFF;
        uint bottom = (bufferTopAndBottom >> 24) & 0xFF;

        if (bottom < FooterSize || bottom > FooterSize + 3 || top < bottom || top > compressedSize)
            throw new InvalidDataException("BackwardLZ77 푸터 값이 유효하지 않습니다. 압축 플래그 오판이거나 데이터가 손상됐을 수 있습니다.");

        uint uncompressedSize = (uint)compressedSize + originalBottom;

        byte[] output = new byte[uncompressedSize];
        Array.Copy(compressed, output, compressedSize);

        int destPos = (int)uncompressedSize;
        int srcPos = compressedSize - (int)bottom;
        int endPos = compressedSize - (int)top;

        while (srcPos - endPos > 0)
        {
            byte flag = output[--srcPos];

            for (int i = 0; i < 8; i++)
            {
                if ((flag << i & 0x80) == 0)
                {
                    if (destPos - endPos < 1 || srcPos - endPos < 1)
                        throw new InvalidDataException("BackwardLZ77 압축 해제 중 데이터 범위를 벗어났습니다.");

                    output[--destPos] = output[--srcPos];
                }
                else
                {
                    if (srcPos - endPos < 2)
                        throw new InvalidDataException("BackwardLZ77 압축 해제 중 데이터 범위를 벗어났습니다.");

                    int a = output[--srcPos];
                    int b = output[--srcPos];

                    int offset = (((a & 0x0F) << 8) | b) + 3;
                    int size = ((a >> 4) & 0x0F) + 3;

                    if (size > destPos - endPos)
                        throw new InvalidDataException("BackwardLZ77 압축 해제 중 데이터 범위를 벗어났습니다.");

                    int dataPos = destPos + offset;

                    if (dataPos > uncompressedSize)
                        throw new InvalidDataException("BackwardLZ77 압축 해제 중 데이터 범위를 벗어났습니다.");

                    for (int j = 0; j < size; j++)
                        output[--destPos] = output[--dataPos];
                }

                if (srcPos - endPos <= 0)
                    break;
            }
        }

        return output;
    }
}