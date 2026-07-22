namespace WiiU.Core.WUP.Services
{
    public class ByteArrayBuffer(int length)
    {
        public byte[] buffer = new byte[length];
        private int lengthOfDataInBuffer;

        public int GetLengthOfDataInBuffer() => lengthOfDataInBuffer;

        public void SetLengthOfDataInBuffer(int lengthOfDataInBuffer) => this.lengthOfDataInBuffer = lengthOfDataInBuffer;

        public int GetSpaceLeft() => buffer.Length - GetLengthOfDataInBuffer();

        public void AddLengthOfDataInBuffer(int bytesRead) => lengthOfDataInBuffer += bytesRead;

        public void ResetLengthOfDataInBuffer() => SetLengthOfDataInBuffer(0);
    }
}
