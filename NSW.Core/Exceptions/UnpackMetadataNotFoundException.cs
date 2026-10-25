using System.IO;

namespace NSW.Core.Exceptions;

public class UnpackMetadataNotFoundException(string message) : FileNotFoundException(message) { }