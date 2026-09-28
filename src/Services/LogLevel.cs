using System;

namespace PingArmor.Services;

/// <summary>
/// Severity of a log entry. Ordered so that a configured minimum level filters lower severities.
/// </summary>
public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3
}

public static class LogLevelExtensions
{
    public static string ToTag(this LogLevel level) => level switch
    {
        LogLevel.Debug => "DEBUG",
        LogLevel.Info => "INFO",
        LogLevel.Warn => "WARN",
        LogLevel.Error => "ERROR",
        _ => "INFO"
    };

    /// <summary>Parses a level name (case-insensitive). Returns <paramref name="fallback"/> when unknown.</summary>
    public static LogLevel Parse(string? value, LogLevel fallback = LogLevel.Debug)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return value.Trim().ToLowerInvariant() switch
        {
            "debug" => LogLevel.Debug,
            "info" or "information" => LogLevel.Info,
            "warn" or "warning" => LogLevel.Warn,
            "error" or "err" => LogLevel.Error,
            _ => fallback
        };
    }

    /// <summary>
    /// Infers a log level from the conventional leading marker used across the codebase
    /// ([+] / [*] => Info, [~] => Debug, [!] => Warn, [-] => Error).
    /// </summary>
    public static LogLevel InferFromMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return LogLevel.Info;

        string trimmed = message.TrimStart();
        if (trimmed.StartsWith("[!]", StringComparison.Ordinal)) return LogLevel.Warn;
        if (trimmed.StartsWith("[-]", StringComparison.Ordinal)) return LogLevel.Error;
        if (trimmed.StartsWith("[~]", StringComparison.Ordinal)) return LogLevel.Debug;
        return LogLevel.Info;
    }
}
