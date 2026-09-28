using System;

namespace PingArmor.Services;

/// <summary>Structured log record used by the in-app log viewer.</summary>
public sealed class LogEntry
{
    public LogEntry(DateTime timestamp, LogLevel level, string message)
    {
        Timestamp = timestamp;
        Level = level;
        Message = message;
    }

    public DateTime Timestamp { get; }
    public LogLevel Level { get; }
    public string Message { get; }

    public string TimeText => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
    public string LevelTag => Level.ToTag();
    public string LevelName => Level.ToString();

    public string ToDisplayString() => $"[{TimeText}] [{LevelTag}] {Message}";
}