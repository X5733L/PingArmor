using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PingArmor.Services;

public static class DnsHelper
{
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

    public static string ConfigureWpadPolicy(bool disableWpad)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true);
            if (key != null)
            {
                if (disableWpad)
                {
                    key.SetValue("AutoDetect", 0, RegistryValueKind.DWord);
                }
                else
                {
                    key.SetValue("AutoDetect", 1, RegistryValueKind.DWord);
                }
            }

            // Also update Connections\DefaultConnectionSettings and SavedLegacySettings byte 8
            using var connKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings\Connections", true);
            if (connKey != null)
            {
                UpdateConnectionSettingsBlob(connKey, "DefaultConnectionSettings", disableWpad);
                UpdateConnectionSettingsBlob(connKey, "SavedLegacySettings", disableWpad);
            }

            RefreshInternetSettings();
        }
        catch { }

        return GetWpadStatusDescription();
    }

    private static void UpdateConnectionSettingsBlob(RegistryKey key, string valueName, bool disableWpad)
    {
        try
        {
            if (key.GetValue(valueName) is byte[] data && data.Length > 8)
            {
                if (disableWpad)
                {
                    data[8] = (byte)(data[8] & ~0x08); // clear auto-detect bit (WPAD disabled)
                }
                else
                {
                    data[8] = (byte)(data[8] | 0x08);  // set auto-detect bit (WPAD enabled)
                }
                key.SetValue(valueName, data, RegistryValueKind.Binary);
            }
        }
        catch { }
    }

    public static string GetWpadStatusDescription()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
            var val = key?.GetValue("AutoDetect");
            if (val is int intVal)
            {
                return intVal == 0
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

    public static string ConfigureSmartDnsPolicy(bool disableSmartDns)
    {
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient", true);
            if (key != null)
            {
                if (disableSmartDns)
                {
                    key.SetValue("DisableSmartNameResolution", 1, RegistryValueKind.DWord);
                }
                else
                {
                    key.DeleteValue("DisableSmartNameResolution", throwOnMissingValue: false);
                }
            }
        }
        catch { }

        return GetSmartDnsStatusDescription();
    }

    public static string GetSmartDnsStatusDescription()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient");
            var val = key?.GetValue("DisableSmartNameResolution");
            if (val is int intVal && intVal == 1)
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

    public static bool ConfigureDnsPolicies(bool disableSmartNameResolution, bool disableWpad)
    {
        ConfigureSmartDnsPolicy(disableSmartNameResolution);
        ConfigureWpadPolicy(disableWpad);
        return true;
    }
}
