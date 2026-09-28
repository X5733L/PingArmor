using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PingArmor.Services;
using Xunit;

namespace PingArmor.Tests;

public class LogServiceTests : IDisposable
{
    private readonly string _testLogPath;

    public LogServiceTests()
    {
        _testLogPath = Path.Combine(Path.GetTempPath(), $"pingarmor_test_{Guid.NewGuid()}.log");
        LogService.LogFilePath = _testLogPath;
        LogService.ClearMemory();
    }

    public void Dispose()
    {
        try
        {
            LogService.Flush();
            if (File.Exists(_testLogPath)) File.Delete(_testLogPath);
            string old = _testLogPath + ".old";
            if (File.Exists(old)) File.Delete(old);
        }
        catch { }
    }

    [Fact]
    public void Log_WritesToMemoryBufferAndFiresEvent()
    {
        bool eventFired = false;
        string? received = null;
        Action<string> handler = msg =>
        {
            eventFired = true;
            received = msg;
        };

        LogService.LogAppended += handler;
        try
        {
            LogService.Log("Test notification message");

            Assert.True(eventFired);
            Assert.NotNull(received);
            Assert.Contains("Test notification message", received);
            Assert.Matches(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\]", received);

            var recent = LogService.GetRecentLogs();
            Assert.NotEmpty(recent);
            Assert.Contains(recent, l => l.Contains("Test notification message"));
        }
        finally
        {
            LogService.LogAppended -= handler;
        }
    }

    [Fact]
    public void Log_FiresStructuredEntryAppended()
    {
        LogEntry? received = null;
        Action<LogEntry> handler = entry => received = entry;

        LogService.EntryAppended += handler;
        try
        {
            LogService.Log(LogLevel.Warn, "structured message");
        }
        finally
        {
            LogService.EntryAppended -= handler;
        }

        Assert.NotNull(received);
        Assert.Equal(LogLevel.Warn, received!.Level);
        Assert.Equal("structured message", received.Message);

        var entries = LogService.GetRecentEntries();
        Assert.Contains(entries, e => e.Message == "structured message" && e.Level == LogLevel.Warn);
    }

    [Fact]
    public void Log_WritesToFileOnDisk()
    {
        LogService.Log("Disk write validation message");
        LogService.Flush();

        Assert.True(File.Exists(_testLogPath));
        string content = File.ReadAllText(_testLogPath);
        Assert.Contains("Disk write validation message", content);
    }

    [Fact]
    public void Log_ConcurrentThreads_LogsSafely()
    {
        Parallel.For(0, 50, i =>
        {
            LogService.Log($"Concurrent test entry {i}");
        });

        LogService.Flush();

        var recent = LogService.GetRecentLogs();
        Assert.True(recent.Count >= 50);
        Assert.True(File.Exists(_testLogPath));
    }

    [Fact]
    public void Log_MemoryBuffer_DoesNotExceedMaxLines()
    {
        for (int i = 0; i < LogService.MaxMemoryLines + 50; i++)
        {
            LogService.Log($"Line {i}");
        }

        var recent = LogService.GetRecentLogs();
        Assert.Equal(LogService.MaxMemoryLines, recent.Count);
    }

    [Fact]
    public void Log_Rotate_WhenFileReachesLimit()
    {
        // Pre-create file with size slightly above 5MB
        byte[] dummyData = new byte[LogService.MaxFileSizeBytes + 10];
        File.WriteAllBytes(_testLogPath, dummyData);

        LogService.Log("Trigger rotation");
        LogService.Flush();

        string oldPath = _testLogPath + ".old";
        Assert.True(File.Exists(oldPath), "Old log file should exist after rotation");
        Assert.True(File.Exists(_testLogPath), "New active log file should exist");
        string newContent = File.ReadAllText(_testLogPath);
        Assert.Contains("Trigger rotation", newContent);
    }

    [Fact]
    public void LogDirectoryPath_ShouldBeValidAndContainLogs()
    {
        string dir = LogService.LogDirectoryPath;
        Assert.NotNull(dir);
        Assert.EndsWith("logs", dir);
        Assert.True(Directory.Exists(dir));
    }

    [Fact]
    public void Log_InfoMessage_IncludesLevelTag()
    {
        LogService.Log(LogLevel.Info, "level info test");

        var recent = LogService.GetRecentLogs();
        Assert.Contains(recent, l => l.Contains("[INFO]") && l.Contains("level info test"));
    }

    [Fact]
    public void Log_ErrorMarker_InfersErrorLevel()
    {
        LogService.Log("[-] something failed");

        var recent = LogService.GetRecentLogs();
        Assert.Contains(recent, l => l.Contains("[ERROR]") && l.Contains("something failed"));
    }

    [Fact]
    public void Log_WarningMarker_InfersWarnLevel()
    {
        LogService.Log("[!] heads up");

        var recent = LogService.GetRecentLogs();
        Assert.Contains(recent, l => l.Contains("[WARN]") && l.Contains("heads up"));
    }

    [Fact]
    public void Log_BelowMinimumLevel_IsFiltered()
    {
        var previous = LogService.MinimumLevel;
        try
        {
            LogService.MinimumLevel = LogLevel.Warn;
            LogService.Log(LogLevel.Info, "should-be-filtered");
            LogService.Log(LogLevel.Warn, "should-be-kept");

            var recent = LogService.GetRecentLogs();
            Assert.DoesNotContain(recent, l => l.Contains("should-be-filtered"));
            Assert.Contains(recent, l => l.Contains("should-be-kept"));
        }
        finally
        {
            LogService.MinimumLevel = previous;
        }
    }
}
