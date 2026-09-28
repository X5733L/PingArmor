using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace PingArmor.Services;

public static class LogService
{
    private static readonly object _lock = new();
    private static readonly List<LogEntry> _entries = new(600);

    /// <summary>Lines waiting to be appended to the file; flushed in batches to reduce file I/O.</summary>
    private static readonly List<string> _pendingWrites = new();

    public const int MaxMemoryLines = 500;
    public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    /// <summary>Periodic flush interval for the write buffer (ms).</summary>
    private const int FlushIntervalMs = 2000;

    /// <summary>Flush immediately once this many lines are buffered.</summary>
    private const int FlushBatchSize = 25;

    public static event Action<string>? LogAppended;

    /// <summary>Raised with a structured record for the in-app log viewer.</summary>
    public static event Action<LogEntry>? EntryAppended;

    private static string? _customLogFilePath;

    /// <summary>
    /// Entries below this severity are discarded. Defaults to <see cref="LogLevel.Debug"/> so
    /// existing behavior is unchanged until a minimum is configured.
    /// </summary>
    public static LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

    public static string LogDirectoryPath
    {
        get
        {
            string baseDir = AppContext.BaseDirectory;
            string logsDir = Path.Combine(baseDir, "logs");
            try
            {
                if (!Directory.Exists(logsDir))
                {
                    Directory.CreateDirectory(logsDir);
                }
                string testFile = Path.Combine(logsDir, $".perm_test_{Guid.NewGuid():N}");
                File.WriteAllText(testFile, string.Empty);
                File.Delete(testFile);
                return logsDir;
            }
            catch
            {
                string localAppDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PingArmor", "logs");
                try
                {
                    if (!Directory.Exists(localAppDir))
                    {
                        Directory.CreateDirectory(localAppDir);
                    }
                }
                catch { }
                return localAppDir;
            }
        }
    }

    public static string LogFilePath
    {
        get
        {
            lock (_lock)
            {
                if (_customLogFilePath != null)
                {
                    return _customLogFilePath;
                }

                string dir = LogDirectoryPath;
                string path = Path.Combine(dir, "pingarmor.log");

                if (!File.Exists(path))
                {
                    string legacyPath = Path.Combine(AppContext.BaseDirectory, "pingarmor.log");
                    if (File.Exists(legacyPath))
                    {
                        try
                        {
                            File.Move(legacyPath, path, overwrite: true);
                        }
                        catch
                        {
                            try
                            {
                                File.Copy(legacyPath, path, overwrite: true);
                                File.Delete(legacyPath);
                            }
                            catch { }
                        }
                    }

                    string legacyOld = Path.Combine(AppContext.BaseDirectory, "pingarmor.log.old");
                    if (File.Exists(legacyOld))
                    {
                        try
                        {
                            File.Move(legacyOld, path + ".old", overwrite: true);
                        }
                        catch { }
                    }
                }

                _customLogFilePath = path;
                return _customLogFilePath;
            }
        }
        set
        {
            lock (_lock)
            {
                // Persist buffered lines to the previous path before switching files.
                FlushToDiskLocked();
                _customLogFilePath = value;
            }
        }
    }

    static LogService()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Flush();

        try
        {
            string path = LogFilePath;
            if (File.Exists(path))
            {
                var lines = File.ReadLines(path).TakeLast(MaxMemoryLines);
                lock (_lock)
                {
                    foreach (string line in lines)
                    {
                        if (TryParseLine(line, out var parsedEntry))
                        {
                            _entries.Add(parsedEntry);
                        }
                    }
                }
            }
        }
        catch { }

        // Bound how long a buffered line can stay out of the file.
        _ = new Timer(_ => Flush(), null, FlushIntervalMs, FlushIntervalMs);
    }

    /// <summary>
    /// Logs a message, inferring its level from the conventional leading marker.
    /// </summary>
    public static void Log(string message)
        => Log(LogLevelExtensions.InferFromMessage(message), message);

    public static void Debug(string message) => Log(LogLevel.Debug, message);
    public static void Info(string message) => Log(LogLevel.Info, message);
    public static void Warn(string message) => Log(LogLevel.Warn, message);
    public static void Error(string message) => Log(LogLevel.Error, message);

    public static void Log(LogLevel level, string message)
    {
        if (level < MinimumLevel) return;

        var entry = new LogEntry(DateTime.Now, level, message);
        string line = entry.ToDisplayString();

        lock (_lock)
        {
            if (_entries.Count >= MaxMemoryLines)
            {
                _entries.RemoveAt(0);
            }
            _entries.Add(entry);

            _pendingWrites.Add(line);
            if (_pendingWrites.Count >= FlushBatchSize)
            {
                FlushToDiskLocked();
            }
        }

        try
        {
            EntryAppended?.Invoke(entry);
        }
        catch { }

        try
        {
            LogAppended?.Invoke(line);
        }
        catch { }
    }

    /// <summary>Writes any buffered lines to disk immediately.</summary>
    public static void Flush()
    {
        lock (_lock)
        {
            FlushToDiskLocked();
        }
    }

    public static IReadOnlyList<string> GetRecentLogs()
    {
        lock (_lock)
        {
            return _entries.Select(e => e.ToDisplayString()).ToList();
        }
    }

    /// <summary>Returns the structured records currently held in memory.</summary>
    public static IReadOnlyList<LogEntry> GetRecentEntries()
    {
        lock (_lock)
        {
            return _entries.ToList();
        }
    }

    public static void ClearMemory()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }

    private static bool TryParseLine(string line, out LogEntry entry)
    {
        entry = null!;
        if (string.IsNullOrEmpty(line) || line[0] != '[') return false;

        int timestampEnd = line.IndexOf(']');
        if (timestampEnd <= 1) return false;
        if (!DateTime.TryParseExact(
                line.Substring(1, timestampEnd - 1),
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime timestamp))
        {
            return false;
        }

        int levelStart = line.IndexOf('[', timestampEnd + 1);
        if (levelStart < 0) return false;
        int levelEnd = line.IndexOf(']', levelStart + 1);
        if (levelEnd < 0) return false;

        LogLevel level = line.Substring(levelStart + 1, levelEnd - levelStart - 1) switch
        {
            "WARN" => LogLevel.Warn,
            "ERROR" => LogLevel.Error,
            "DEBUG" => LogLevel.Debug,
            _ => LogLevel.Info
        };

        string message = line.Substring(levelEnd + 1).TrimStart();
        entry = new LogEntry(timestamp, level, message);
        return true;
    }

    /// <summary>Appends the buffered lines in a single file operation. Caller must hold <see cref="_lock"/>.</summary>
    private static void FlushToDiskLocked()
    {
        if (_pendingWrites.Count == 0) return;

        try
        {
            string path = LogFilePath;
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            long currentSize = File.Exists(path) ? new FileInfo(path).Length : 0;
            long pendingBytes = _pendingWrites.Sum(l => (long)l.Length + Environment.NewLine.Length);

            if (currentSize + pendingBytes >= MaxFileSizeBytes)
            {
                RotateLogs(path);
            }

            File.AppendAllLines(path, _pendingWrites);
        }
        catch
        {
            // Suppress logging I/O failures so app continues operating uninterrupted
        }
        finally
        {
            _pendingWrites.Clear();
        }
    }

    private static void RotateLogs(string path)
    {
        try
        {
            string oldPath = path + ".old";
            if (File.Exists(oldPath))
            {
                File.Delete(oldPath);
            }
            File.Move(path, oldPath);
        }
        catch
        {
            try
            {
                // Fallback: truncate active log if rename/move is blocked
                File.WriteAllText(path, string.Empty);
            }
            catch { }
        }
    }
}