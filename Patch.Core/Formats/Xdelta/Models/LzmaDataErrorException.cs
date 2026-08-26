namespace Patch.Core.Formats.Xdelta.Models;

public class LzmaDataErrorException : LzmaException
{
    public LzmaDataErrorException() : base("Compressed data is corrupt.") { }

    public LzmaDataErrorException(string message) : base(message) { }
}