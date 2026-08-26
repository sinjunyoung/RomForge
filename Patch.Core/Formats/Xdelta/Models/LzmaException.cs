namespace Patch.Core.Formats.Xdelta.Models;

public class LzmaException : Exception
{
    public LzmaException() { }

    public LzmaException(string message) : base(message) { }

    public LzmaException(string message, Exception innerException) : base(message, innerException) { }
}