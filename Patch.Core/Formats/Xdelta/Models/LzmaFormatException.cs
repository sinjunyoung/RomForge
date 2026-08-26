namespace Patch.Core.Formats.Xdelta.Models;

public class LzmaFormatException : LzmaException
{
    public LzmaFormatException() : base("Input format not recognized.") { }

    public LzmaFormatException(string message) : base(message) { }
}