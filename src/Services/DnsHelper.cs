using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PingArmor.Services;

public static class DnsHelper
{
    internal const string WpadSettingsKey = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
    internal const string WpadConnectionsKey = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings\Connections";
    internal const string SmartDnsPolicyKey = @"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient";
    internal const string SmartDnsValueName = "DisableSmartNameResolution";

    [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
    private static extern int DnsFlushResolverCache();

    [DllImport("wininet.dll", SetLastError = true)]
    private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

    private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
    private const int INTERNET_OPTION_REFRESH = 37;

    public static void RefreshInternetSettings()
    {
        try
        {
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
        }
        catch { }
    }

    public static bool FlushDnsCache()
    {
        try
        {
            int result = DnsFlushResolverCache();
            return result != 0;
        }
        catch
        {
            return false;
        }
    }

    public static string ConfigureWpadPolicy(bool disableWpad, IRegistryAccessor? registry = null)
    {
        registry ??= RegistryAccessor.Default;
        try
        {
            registry.WriteDword(RegistryHive.CurrentUser, WpadSettingsKey, "AutoDetect", disableWpad ? 0 : 1);

            // Also update Connections\DefaultConnectionSettings and SavedLegacySettings byte 8
            UpdateConnectionSettingsBlob(registry, "DefaultConnectionSettings", disableWpad);
            UpdateConnectionSettingsBlob(registry, "SavedLegacySettings", disableWpad);

            RefreshInternetSettings();
        }
        catch { }

        return GetWpadStatusDescription(registry);
    }

    private static void UpdateConnectionSettingsBlob(IRegistryAccessor registry, string valueName, bool disableWpad)
    {
        try
        {
            if (registry.ReadBinary(RegistryHive.CurrentUser, WpadConnectionsKey, valueName) is { Length: > 8 } data)
            {
                if (disableWpad)
                {
                    data[8] = (byte)(data[8] & ~0x08); // clear auto-detect bit (WPAD disabled)
                }
                else
                {
                    data[8] = (byte)(data[8] | 0x08);  // set auto-detect bit (WPAD enabled)
                }
                registry.WriteBinary(RegistryHive.CurrentUser, WpadConnectionsKey, valueName, data);
            }
        }
        catch { }
    }

    public static string GetWpadStatusDescription(IRegistryAccessor? registry = null)
    {
        registry ??= RegistryAccessor.Default;
        try
        {
            var val = registry.ReadDword(RegistryHive.CurrentUser, WpadSettingsKey, "AutoDetect");
            if (val.HasValue)
            {
                return val.Value == 0
                    ? "HKCU\\...\\Internet Settings\\AutoDetect = 0 (WPAD proxy auto-detection is DISABLED)"
                    : "HKCU\\...\\Internet Settings\\AutoDetect = 1 (WPAD proxy auto-detection is ENABLED)";
            }
            return "HKCU\\...\\Internet Settings\\AutoDetect = [not set] (WPAD proxy auto-detection is ENABLED [system default])";
        }
        catch (Exception ex)
        {
            return $"Error reading WPAD status: {ex.Message}";
        }
    }

    public static string ConfigureSmartDnsPolicy(bool disableSmartDns, IRegistryAccessor? registry = null)
    {
        registry ??= RegistryAccessor.Default;
        try
        {
            if (disableSmartDns)
            {
                registry.WriteDword(RegistryHive.LocalMachine, SmartDnsPolicyKey, SmartDnsValueName, 1);
            }
            else
            {
                registry.DeleteValue(RegistryHive.LocalMachine, SmartDnsPolicyKey, SmartDnsValueName);
            }
        }
        catch { }

        return GetSmartDnsStatusDescription(registry);
    }

    public static string GetSmartDnsStatusDescription(IRegistryAccessor? registry = null)
    {
        registry ??= RegistryAccessor.Default;
        try
        {
            var val = registry.ReadDword(RegistryHive.LocalMachine, SmartDnsPolicyKey, SmartDnsValueName);
            if (val == 1)
            {
                return "HKLM\\...\\DNSClient\\DisableSmartNameResolution = 1 (Smart Name Resolution is DISABLED)";
            }
            return "HKLM\\...\\DNSClient\\DisableSmartNameResolution = [default] (Smart Name Resolution is ENABLED)";
        }
        catch (Exception ex)
        {
            return $"Error reading Smart DNS status: {ex.Message}";
        }
    }

    public static bool ConfigureDnsPolicies(bool disableSmartNameResolution, bool disableWpad, IRegistryAccessor? registry = null)
    {
        ConfigureSmartDnsPolicy(disableSmartNameResolution, registry);
        ConfigureWpadPolicy(disableWpad, registry);
        return true;
    }
}
