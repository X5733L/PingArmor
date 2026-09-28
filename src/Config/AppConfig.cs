using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PingArmor.Config;

public class AppConfig
{
    public int CheckIntervalSeconds { get; set; } = 30;
    public bool EnableMetricOptimization { get; set; } = true;
    public int PrimaryEthernetMetric { get; set; } = 5;
    public int PrimaryWifiMetric { get; set; } = 10;
    public int VirtualAdapterMetric { get; set; } = 500;
    public int DisconnectedAdapterMetric { get; set; } = 100;
    public int SecondaryPhysicalMetric { get; set; } = 50;
    public bool DisableSmartNameResolution { get; set; } = true;
    public bool DisableWpad { get; set; } = true;
    public bool FlushDnsOnChange { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public bool DisableIPv6OnWifi { get; set; } = true;

    /// <summary>
    /// WLAN Optimizer: disables Windows background Wi-Fi scanning while running to eliminate latency spikes in gaming and real-time apps.
    /// </summary>
    public bool EnableWlanOptimizer { get; set; } = true;

    /// <summary>
    /// Reverts all network metrics, DNS registry policies, and IPv6 bindings to their original state upon closing PingArmor.
    /// </summary>
    public bool RestoreOnExit { get; set; } = true;
    public string Language { get; set; } = "ru";

    /// <summary>
    /// Interface theme: System | Light | Dark. Applied through UI/Services/ThemeService.cs.
    /// </summary>
    public string Theme { get; set; } = "System";

    public List<string> ExcludeAdapters { get; set; } = new();

    /// <summary>
    /// Minimum severity written to the log file: debug | info | warn | error.
    /// </summary>
    public string LogLevel { get; set; } = "debug";

    [JsonIgnore]
    private readonly object _excludeLock = new();

    public static string GetDefaultConfigDirectory()
    {
        string baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "configs");
    }

    public static string GetDefaultConfigPath()
    {
        return Path.Combine(GetDefaultConfigDirectory(), "config.json");
    }

    /// <summary>
    /// Per-user fallback location used when the application directory is read-only
    /// (e.g. installed under Program Files). Written and read back consistently.
    /// </summary>
    public static string GetFallbackConfigPath()
    {
        string localDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PingArmor",
            "configs");
        return Path.Combine(localDir, "config.json");
    }

    /// <summary>
    /// Returns a thread-safe copy of the adapter exclusion list.
    /// The monitor thread enumerates this while the UI thread may mutate it.
    /// </summary>
    public List<string> GetExcludeSnapshot()
    {
        lock (_excludeLock)
        {
            return new List<string>(ExcludeAdapters ?? new List<string>());
        }
    }

    /// <summary>Adds an adapter to the exclusion list in a thread-safe way.</summary>
    /// <returns>True if the entry was newly added.</returns>
    public bool AddExclusion(string adapter)
    {
        if (string.IsNullOrWhiteSpace(adapter)) return false;
        lock (_excludeLock)
        {
            ExcludeAdapters ??= new List<string>();
            if (ExcludeAdapters.Contains(adapter, StringComparer.OrdinalIgnoreCase)) return false;
            ExcludeAdapters.Add(adapter);
            return true;
        }
    }

    /// <summary>Removes an adapter from the exclusion list in a thread-safe way.</summary>
    /// <returns>True if an entry was removed.</returns>
    public bool RemoveExclusion(string adapter)
    {
        if (string.IsNullOrWhiteSpace(adapter)) return false;
        lock (_excludeLock)
        {
            return ExcludeAdapters?.RemoveAll(x => x.Equals(adapter, StringComparison.OrdinalIgnoreCase)) > 0;
        }
    }

    public static AppConfig Load(string? path = null)
    {
        if (path == null)
        {
            path = ResolveConfigPath();
        }

        AppConfig config;
        if (!File.Exists(path))
        {
            config = new AppConfig();
            try
            {
                config.Save(path);
            }
            catch
            {
                // Ignore if cannot save to directory (e.g. read-only)
            }
        }
        else
        {
            try
            {
                string json = File.ReadAllText(path);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };
                config = JsonSerializer.Deserialize<AppConfig>(json, options) ?? new AppConfig();
            }
            catch
            {
                // Corrupt config: rename it aside so it is not silently lost, then use defaults.
                TryQuarantineCorruptConfig(path);
                config = new AppConfig();
            }
        }

        return config;
    }

    /// <summary>
    /// Resolves the configuration path, migrating a legacy root-level config.json when present
    /// and falling back to the per-user location for read-only installs.
    /// </summary>
    private static string ResolveConfigPath()
    {
        string defaultPath = GetDefaultConfigPath();
        if (File.Exists(defaultPath)) return defaultPath;

        // Legacy layout: config.json next to the executable.
        string legacyPath = Path.Combine(AppContext.BaseDirectory, "config.json");
        if (File.Exists(legacyPath))
        {
            try
            {
                string dir = Path.GetDirectoryName(defaultPath)!;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.Move(legacyPath, defaultPath, overwrite: true);
                return defaultPath;
            }
            catch
            {
                return legacyPath;
            }
        }

        // Per-user fallback written previously because the install dir was read-only.
        string fallbackPath = GetFallbackConfigPath();
        if (File.Exists(fallbackPath)) return fallbackPath;

        return defaultPath;
    }

    private static void TryQuarantineCorruptConfig(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            string quarantine = path + $".corrupt-{DateTime.Now:yyyyMMddHHmmss}";
            File.Move(path, quarantine, overwrite: false);
        }
        catch
        {
            // Best effort only.
        }
    }

    public void Save(string? path = null)
    {
        path ??= ResolveConfigPath();
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        string json = JsonSerializer.Serialize(this, options);

        try
        {
            AtomicWrite(path, json);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or NotSupportedException)
        {
            // Read-only install directory: fall back to the per-user location.
            string fallbackPath = GetFallbackConfigPath();
            string fallbackDir = Path.GetDirectoryName(fallbackPath)!;
            Directory.CreateDirectory(fallbackDir);
            AtomicWrite(fallbackPath, json);
        }
    }

    private static void AtomicWrite(string path, string contents)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, contents);
        File.Move(tempPath, path, overwrite: true);
    }
}
