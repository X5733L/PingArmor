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
    public void Log_WritesToFileOnDisk()
    {
        LogService.Log("Disk write validation message");

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
}
