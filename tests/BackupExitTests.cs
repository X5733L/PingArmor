using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PingArmor.Config;
using PingArmor.Models;
using PingArmor.Services;
using Xunit;

namespace PingArmor.Tests;

public class BackupExitTests : IDisposable
{
    private readonly string _tempBackupPath;

    public BackupExitTests()
    {
        _tempBackupPath = Path.Combine(Path.GetTempPath(), $"test_exit_backup_{Guid.NewGuid()}.json");
    }

    [Fact]
    public void AppConfig_DefaultValues_HaveRestoreOnExitTrue_And30SecInterval()
    {
        var config = new AppConfig();
        Assert.True(config.RestoreOnExit);
        Assert.Equal(30, config.CheckIntervalSeconds);
    }

    [Fact]
    public void RestoreFromBackup_WithDeleteBackupTrue_DeletesFileAfterRestore()
    {
        // Arrange: create a backup snapshot
        var snapshot = new NetworkBackupSnapshot
        {
            CreatedAt = DateTime.Now,
            MachineName = "TestMachine",
            Adapters = new(),
            Ipv6Bindings = new(),
            Registry = new RegistryBackup()
        };
        string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_tempBackupPath, json);

        Assert.True(File.Exists(_tempBackupPath));

        // Act: restore with deleteBackupAfterRestore = true
        // Inject fakes so the test never mutates the host registry or spawns processes.
        var logs = BackupService.RestoreFromBackup(
            _tempBackupPath,
            deleteBackupAfterRestore: true,
            processRunner: new NoopProcessRunner(),
            registry: new InMemoryRegistryAccessor());

        // Assert
        Assert.False(File.Exists(_tempBackupPath));
        Assert.Contains(logs, l => l.Contains("Backup file deleted after successful restoration"));
    }

    [Fact]
    public void RestoreFromBackup_WithDeleteBackupFalse_PreservesFile()
    {
        // Arrange: create a backup snapshot
        var snapshot = new NetworkBackupSnapshot
        {
            CreatedAt = DateTime.Now,
            MachineName = "TestMachine",
            Adapters = new(),
            Ipv6Bindings = new(),
            Registry = new RegistryBackup()
        };
        string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_tempBackupPath, json);

        Assert.True(File.Exists(_tempBackupPath));

        // Act: restore with deleteBackupAfterRestore = false
        var logs = BackupService.RestoreFromBackup(
            _tempBackupPath,
            deleteBackupAfterRestore: false,
            processRunner: new NoopProcessRunner(),
            registry: new InMemoryRegistryAccessor());

        // Assert: file should still exist
        Assert.True(File.Exists(_tempBackupPath));
        Assert.DoesNotContain(logs, l => l.Contains("Backup file deleted"));
    }

    [Fact]
    public void BackupService_HasRestoredOnExit_CanBeToggled()
    {
        BackupService.HasRestoredOnExit = false;
        Assert.False(BackupService.HasRestoredOnExit);
        BackupService.HasRestoredOnExit = true;
        Assert.True(BackupService.HasRestoredOnExit);
        BackupService.HasRestoredOnExit = false;
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempBackupPath))
            {
                File.Delete(_tempBackupPath);
            }
        }
        catch { }
    }
}

/// <summary>
/// Process runner stub that never touches the host system.
/// </summary>
internal sealed class NoopProcessRunner : IProcessRunner
{
    public ProcessResult Run(string fileName, string arguments, int timeoutMs) =>
        new(0, string.Empty, string.Empty, false);

    public Task<ProcessResult> RunAsync(string fileName, string arguments, int timeoutMs, CancellationToken cancellationToken = default) =>
        Task.FromResult(Run(fileName, arguments, timeoutMs));
}
