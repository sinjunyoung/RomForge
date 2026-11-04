using Common.Models;

namespace RomForge.Core.Models;

public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public string FormattedTime => Timestamp.ToString("HH:mm:ss");

    public required string Message { get; set; }

    public LogLevel Level { get; set; }
}