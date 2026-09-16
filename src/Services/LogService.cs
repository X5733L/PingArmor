using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PingArmor.Services;

public static class LogService
{
    private static readonly object _lock = new();
    private static readonly List<string> _memoryBuffer = new(1000);
    public const int MaxMemoryLines = 2000;
    public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    public static event Action<string>? LogAppended;

    private static string? _customLogFilePath;

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
                _customLogFilePath = value;
            }
        }
    }

    static LogService()
    {
        try
        {
            string path = LogFilePath;
            if (File.Exists(path))
            {
                var lines = File.ReadLines(path).TakeLast(500);
                lock (_lock)
                {
                    _memoryBuffer.AddRange(lines);
                }
            }
        }
        catch { }
    }

    public static void Log(string message)
    {
        string entry = $"[{DateTime.Now:HH:mm:ss}] {message}";

        lock (_lock)
        {
            if (_memoryBuffer.Count >= MaxMemoryLines)
            {
                _memoryBuffer.RemoveAt(0);
            }
            _memoryBuffer.Add(entry);

            WriteToFile(entry);
        }

        try
        {
            LogAppended?.Invoke(entry);
        }
        catch { }
    }

    public static IReadOnlyList<string> GetRecentLogs()
    {
        lock (_lock)
        {
            return _memoryBuffer.ToList();
        }
    }

    public static void ClearMemory()
    {
        lock (_lock)
        {
            _memoryBuffer.Clear();
        }
    }

    private static void WriteToFile(string entry)
    {
        try
        {
            string path = LogFilePath;
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var fi = new FileInfo(path);
            if (fi.Exists && fi.Length >= MaxFileSizeBytes)
            {
                RotateLogs(path);
            }

            File.AppendAllText(path, entry + Environment.NewLine);
        }
        catch
        {
            // Suppress logging I/O failures so app continues operating uninterrupted
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
