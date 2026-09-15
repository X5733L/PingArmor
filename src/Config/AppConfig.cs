using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PingArmor.Config;

public class AppConfig
{
    public int CheckIntervalSeconds { get; set; } = 30;
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
    public List<string> ExcludeAdapters { get; set; } = new();

    public static string GetDefaultConfigPath()
    {
        string baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "config.json");
    }

    public static AppConfig Load(string? path = null)
    {
        path ??= GetDefaultConfigPath();
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
                config = new AppConfig();
            }
        }

        return config;
    }

    public void Save(string? path = null)
    {
        path ??= GetDefaultConfigPath();
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        string json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(path, json);
    }
}
