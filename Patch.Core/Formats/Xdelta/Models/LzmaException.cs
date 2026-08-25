namespace Patch.Core.Formats.Xdelta.Models;

public class LzmaException : Exception
{
    public LzmaException() { }

    public LzmaException(string message) : base(message) { }

    public LzmaException(string message, Exception innerException) : base(message, innerException) { }
}

public class LzmaDataErrorException : LzmaException
{
    public LzmaDataErrorException() : base("Compressed data is corrupt.") { }

    public LzmaDataErrorException(string message) : base(message) { }
}

public class LzmaMemoryLimitException : LzmaException
{
    public LzmaMemoryLimitException() : base("Decompressed output would exceed the configured memory limit.") { }

    public LzmaMemoryLimitException(string message) : base(message) { }
}

public class LzmaFormatException : LzmaException
{
    public LzmaFormatException() : base("Input format not recognized.") { }

    public LzmaFormatException(string message) : base(message) { }
}