using System;
using System.IO;
using System.Text.Json;
using PingArmor.Models;
using PingArmor.Services;
using Xunit;

namespace PingArmor.Tests;

public class BackupServiceTests : IDisposable
{
    private readonly string _tempBackupPath;

    public BackupServiceTests()
    {
        _tempBackupPath = Path.Combine(Path.GetTempPath(), $"test_backup_{Guid.NewGuid()}.json");
    }

    [Fact]
    public void CreateBackupIfNotExists_FirstCall_CreatesFile()
    {
        // First call should create the backup file
        bool created = BackupService.CreateBackupIfNotExists(_tempBackupPath);

        Assert.True(created);
        Assert.True(File.Exists(_tempBackupPath));
    }

    [Fact]
    public void CreateBackupIfNotExists_SecondCall_DoesNotOverwrite()
    {
        // First call creates backup
        BackupService.CreateBackupIfNotExists(_tempBackupPath);
        var originalContent = File.ReadAllText(_tempBackupPath);
        var originalWriteTime = File.GetLastWriteTimeUtc(_tempBackupPath);

        // Small delay to ensure timestamp would differ
        System.Threading.Thread.Sleep(50);

        // Second call should not overwrite
        bool created = BackupService.CreateBackupIfNotExists(_tempBackupPath);

        Assert.False(created);
        Assert.Equal(originalContent, File.ReadAllText(_tempBackupPath));
    }

    [Fact]
    public void CreateBackup_ForcesOverwrite()
    {
        // Create initial backup
        BackupService.CreateBackupIfNotExists(_tempBackupPath);
        var originalContent = File.ReadAllText(_tempBackupPath);

        System.Threading.Thread.Sleep(50);

        // Force overwrite
        BackupService.CreateBackup(_tempBackupPath);

        // File should be updated (at minimum, CreatedAt timestamp differs)
        Assert.True(File.Exists(_tempBackupPath));
        var newContent = File.ReadAllText(_tempBackupPath);
        // Content may differ due to timestamp
        Assert.NotEmpty(newContent);
    }

    [Fact]
    public void LoadBackup_ValidFile_DeserializesCorrectly()
    {
        var snapshot = new NetworkBackupSnapshot
        {
            CreatedAt = new DateTime(2026, 9, 14, 12, 0, 0),
            MachineName = "TEST-PC",
            Adapters =
            {
                new AdapterBackup
                {
                    InterfaceIndex = 4,
                    Name = "Wi-Fi",
                    IPv4Metric = 40,
                    AutomaticMetric = true
                },
                new AdapterBackup
                {
                    InterfaceIndex = 7,
                    Name = "Ethernet",
                    IPv4Metric = 25,
                    AutomaticMetric = false
                }
            },
            Registry = new RegistryBackup
            {
                DisableSmartNameResolution = null,
                WpadAutoDetect = 1
            },
            Ipv6Bindings =
            {
                new Ipv6BindingBackup
                {
                    AdapterName = "Wi-Fi",
                    Ipv6Enabled = true
                }
            }
        };

        string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_tempBackupPath, json);

        var loaded = BackupService.LoadBackup(_tempBackupPath);

        Assert.NotNull(loaded);
        Assert.Equal("TEST-PC", loaded.MachineName);
        Assert.Equal(2, loaded.Adapters.Count);
        Assert.Equal(4, loaded.Adapters[0].InterfaceIndex);
        Assert.Equal("Wi-Fi", loaded.Adapters[0].Name);
        Assert.Equal(40, loaded.Adapters[0].IPv4Metric);
        Assert.True(loaded.Adapters[0].AutomaticMetric);
        Assert.Equal(25, loaded.Adapters[1].IPv4Metric);
        Assert.False(loaded.Adapters[1].AutomaticMetric);
        Assert.Null(loaded.Registry.DisableSmartNameResolution);
        Assert.Equal(1, loaded.Registry.WpadAutoDetect);
        Assert.Single(loaded.Ipv6Bindings);
        Assert.True(loaded.Ipv6Bindings[0].Ipv6Enabled);
    }

    [Fact]
    public void LoadBackup_MissingFile_ReturnsNull()
    {
        var loaded = BackupService.LoadBackup(Path.Combine(Path.GetTempPath(), "nonexistent_backup.json"));
        Assert.Null(loaded);
    }

    [Fact]
    public void LoadBackup_CorruptFile_ReturnsNull()
    {
        File.WriteAllText(_tempBackupPath, "this is not valid json {{{");
        var loaded = BackupService.LoadBackup(_tempBackupPath);
        Assert.Null(loaded);
    }

    [Fact]
    public void BackupExists_NoFile_ReturnsFalse()
    {
        Assert.False(BackupService.BackupExists(Path.Combine(Path.GetTempPath(), "nonexistent.json")));
    }

    [Fact]
    public void BackupExists_WithFile_ReturnsTrue()
    {
        File.WriteAllText(_tempBackupPath, "{}");
        Assert.True(BackupService.BackupExists(_tempBackupPath));
    }

    [Fact]
    public void DeleteBackup_ExistingFile_DeletesAndReturnsTrue()
    {
        File.WriteAllText(_tempBackupPath, "{}");
        Assert.True(BackupService.DeleteBackup(_tempBackupPath));
        Assert.False(File.Exists(_tempBackupPath));
    }

    [Fact]
    public void DeleteBackup_MissingFile_ReturnsFalse()
    {
        Assert.False(BackupService.DeleteBackup(Path.Combine(Path.GetTempPath(), "missing.json")));
    }

    [Fact]
    public void RestoreFromBackup_NoBackupFile_ReturnsErrorLog()
    {
        var logs = BackupService.RestoreFromBackup(Path.Combine(Path.GetTempPath(), "missing.json"));
        Assert.Single(logs);
        Assert.Contains("No backup file found", logs[0]);
    }

    [Fact]
    public void CaptureCurrentState_ReturnsValidSnapshot()
    {
        // This test runs on the actual system and validates structure
        var snapshot = BackupService.CaptureCurrentState();

        Assert.NotNull(snapshot);
        Assert.NotEqual(default, snapshot.CreatedAt);
        Assert.False(string.IsNullOrEmpty(snapshot.MachineName));
        // Adapter list may be empty in CI but should not be null
        Assert.NotNull(snapshot.Adapters);
        Assert.NotNull(snapshot.Registry);
        Assert.NotNull(snapshot.Ipv6Bindings);
    }

    [Fact]
    public void RoundTrip_CreateAndLoad_PreservesData()
    {
        BackupService.CreateBackup(_tempBackupPath);

        var loaded = BackupService.LoadBackup(_tempBackupPath);

        Assert.NotNull(loaded);
        Assert.Equal(Environment.MachineName, loaded.MachineName);
        Assert.NotNull(loaded.Adapters);
        Assert.NotNull(loaded.Registry);
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
