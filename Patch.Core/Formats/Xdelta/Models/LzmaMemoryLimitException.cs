namespace Patch.Core.Formats.Xdelta.Models;

public class LzmaMemoryLimitException : LzmaException
{
    public LzmaMemoryLimitException() : base("Decompressed output would exceed the configured memory limit.") { }

    public LzmaMemoryLimitException(string message) : base(message) { }
}