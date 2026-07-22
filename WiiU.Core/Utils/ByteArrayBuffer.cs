namespace NUSPacker.Utils
{
    public class ByteArrayBuffer
    {
        public byte[] buffer;
        private int lengthOfDataInBuffer;

        public ByteArrayBuffer(int length)
        {
            buffer = new byte[length];
        }

        public int GetLengthOfDataInBuffer()
        {
            return lengthOfDataInBuffer;
        }

        public void SetLengthOfDataInBuffer(int lengthOfDataInBuffer)
        {
            this.lengthOfDataInBuffer = lengthOfDataInBuffer;
        }

        public int GetSpaceLeft()
        {
            return buffer.Length - GetLengthOfDataInBuffer();
        }

        public void AddLengthOfDataInBuffer(int bytesRead)
        {
            lengthOfDataInBuffer += bytesRead;
        }

        public void ResetLengthOfDataInBuffer()
        {
            SetLengthOfDataInBuffer(0);
        }
    }
}
