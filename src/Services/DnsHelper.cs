using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PingArmor.Services;

public static class DnsHelper
{
    [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
    private static extern int DnsFlushResolverCache();

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

    public static bool ConfigureDnsPolicies(bool disableSmartNameResolution, bool disableWpad)
    {
        bool success = true;

        if (disableSmartNameResolution)
        {
            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient", true);
                if (key != null)
                {
                    key.SetValue("DisableSmartNameResolution", 1, RegistryValueKind.DWord);
                }
            }
            catch
            {
                success = false;
            }
        }

        if (disableWpad)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true);
                if (key != null)
                {
                    key.SetValue("AutoDetect", 0, RegistryValueKind.DWord);
                }
            }
            catch
            {
                success = false;
            }
        }

        return success;
    }
}
