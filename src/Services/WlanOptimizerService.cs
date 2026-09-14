using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PingArmor.Services;

public class WlanOptimizationResult
{
    public bool Success { get; set; }
    public bool IsGamingModeActive { get; set; }
    public List<string> AffectedInterfaces { get; set; } = new();
    public List<string> Logs { get; set; } = new();
}

public static class WlanInterfaceParser
{
    private static readonly Regex InterfaceNameRegex = new(
        @"^\s*(?:Имя|Name)\s*:\s*(.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase
    );

    public static List<string> ParseInterfaceNames(string? netshOutput)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(netshOutput)) return list;

        var matches = InterfaceNameRegex.Matches(netshOutput);
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                string name = match.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(name) && !list.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(name);
                }
            }
        }

        return list;
    }
}

public static class WlanOptimizerService
{
    private static readonly object _lock = new();
    private static IntPtr _wlanHandle = IntPtr.Zero;
    private static readonly HashSet<Guid> _optimizedGuids = new();

    public static bool IsGamingModeActive { get; private set; }

    #region P/Invoke Native Wifi API (wlanapi.dll)

    private const int WLAN_API_VERSION_2_0 = 2;

    // WLAN_INTF_OPCODE from wlanapi.h
    public const int wlan_intf_opcode_autoconf_enabled = 1;
    public const int wlan_intf_opcode_background_scan_enabled = 2;
    public const int wlan_intf_opcode_media_streaming_mode = 3;
    public const int wlan_intf_opcode_interface_state = 6;

    // WLAN_INTERFACE_STATE
    public const int wlan_interface_state_not_ready = 0;
    public const int wlan_interface_state_connected = 1;
    public const int wlan_interface_state_ad_hoc_network_formed = 2;
    public const int wlan_interface_state_disconnecting = 3;
    public const int wlan_interface_state_disconnected = 4;
    public const int wlan_interface_state_associating = 5;
    public const int wlan_interface_state_discovering = 6;
    public const int wlan_interface_state_authenticating = 7;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_INTERFACE_INFO
    {
        public Guid InterfaceGuid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strInterfaceDescription;
        public int isState;
    }

    [DllImport("wlanapi.dll", SetLastError = true)]
    private static extern int WlanOpenHandle(
        uint dwClientVersion,
        IntPtr pReserved,
        out uint pdwNegotiatedVersion,
        out IntPtr phClientHandle
    );

    [DllImport("wlanapi.dll", SetLastError = true)]
    private static extern int WlanCloseHandle(
        IntPtr hClientHandle,
        IntPtr pReserved
    );

    [DllImport("wlanapi.dll", SetLastError = true)]
    private static extern int WlanEnumInterfaces(
        IntPtr hClientHandle,
        IntPtr pReserved,
        out IntPtr ppInterfaceList
    );

    [DllImport("wlanapi.dll", SetLastError = true)]
    private static extern int WlanSetInterface(
        IntPtr hClientHandle,
        ref Guid pInterfaceGuid,
        int OpCode,
        uint dwDataSize,
        IntPtr pData,
        IntPtr pReserved
    );

    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(IntPtr pMemory);

    #endregion

    static WlanOptimizerService()
    {
        // Seamlessly restore default background scan on application exit
        AppDomain.CurrentDomain.ProcessExit += (_, _) => RestoreDefaultScan();
    }

    private static IntPtr GetHandle()
    {
        if (_wlanHandle == IntPtr.Zero)
        {
            int res = WlanOpenHandle(WLAN_API_VERSION_2_0, IntPtr.Zero, out _, out _wlanHandle);
            if (res != 0)
            {
                _wlanHandle = IntPtr.Zero;
            }
        }
        return _wlanHandle;
    }

    private static List<WLAN_INTERFACE_INFO> GetNativeInterfaces(IntPtr handle)
    {
        var list = new List<WLAN_INTERFACE_INFO>();
        if (handle == IntPtr.Zero) return list;

        int res = WlanEnumInterfaces(handle, IntPtr.Zero, out IntPtr pList);
        if (res == 0 && pList != IntPtr.Zero)
        {
            try
            {
                int count = Marshal.ReadInt32(pList, 0);
                for (int i = 0; i < count; i++)
                {
                    IntPtr pInfo = new IntPtr(pList.ToInt64() + 8 + i * 532);
                    var info = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(pInfo);
                    list.Add(info);
                }
            }
            finally
            {
                WlanFreeMemory(pList);
            }
        }

        return list;
    }

    /// <summary>
    /// Checks whether any Wi-Fi adapter is currently connected.
    /// </summary>
    public static bool IsAnyWifiConnected()
    {
        // 1. Fast check via .NET NetworkInterface
        try
        {
            bool netConnected = NetworkInterface.GetAllNetworkInterfaces()
                .Any(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 &&
                          n.OperationalStatus == OperationalStatus.Up);
            if (netConnected) return true;
        }
        catch { }

        // 2. Check via Native Wifi API
        IntPtr handle = GetHandle();
        if (handle != IntPtr.Zero)
        {
            var ifaces = GetNativeInterfaces(handle);
            return ifaces.Any(i => i.isState == wlan_interface_state_connected);
        }

        return false;
    }

    /// <summary>
    /// Enables or disables Wi-Fi gaming / anti-lag optimization.
    /// Uses WlanSetInterface (OpCode 2 - background_scan_enabled and OpCode 3 - media_streaming_mode).
    /// Does not reset or interrupt the active Wi-Fi connection.
    /// </summary>
    public static WlanOptimizationResult SetGamingMode(bool enableGamingMode)
    {
        lock (_lock)
        {
            var result = new WlanOptimizationResult();
            IntPtr handle = GetHandle();

            if (handle == IntPtr.Zero)
            {
                // Fallback to netsh if Native API is unavailable
                return SetGamingModeFallback(enableGamingMode);
            }

            var ifaces = GetNativeInterfaces(handle);
            if (ifaces.Count == 0)
            {
                result.Success = false;
                result.Logs.Add("[-] No Wi-Fi wireless interfaces detected.");
                return result;
            }

            // Smart connection check:
            // If enabling optimization but the Wi-Fi adapter hasn't connected yet (e.g. at PC boot),
            // do not block scanning yet so Windows can discover and connect to the network.
            if (enableGamingMode)
            {
                bool anyConnected = ifaces.Any(i => i.isState == wlan_interface_state_connected);
                if (!anyConnected)
                {
                    result.Success = true;
                    result.IsGamingModeActive = false;
                    result.Logs.Add("[~] Wi-Fi еще подключается к сети. Игровой режим активируется сразу после установки соединения.");
                    return result;
                }
            }

            IntPtr pBoolFalse = Marshal.AllocHGlobal(sizeof(int));
            IntPtr pBoolTrue = Marshal.AllocHGlobal(sizeof(int));
            Marshal.WriteInt32(pBoolFalse, 0); // FALSE
            Marshal.WriteInt32(pBoolTrue, 1);  // TRUE

            try
            {
                bool allOk = true;

                foreach (var iface in ifaces)
                {
                    var guid = iface.InterfaceGuid;
                    string desc = iface.strInterfaceDescription;

                    if (enableGamingMode)
                    {
                        // Apply only to connected interfaces
                        if (iface.isState != wlan_interface_state_connected) continue;

                        // 1. Disable background scanning (OpCode = 2) without dropping connection
                        int resScan = WlanSetInterface(handle, ref guid, wlan_intf_opcode_background_scan_enabled, sizeof(int), pBoolFalse, IntPtr.Zero);

                        // 2. Enable media streaming mode for minimal jitter (OpCode = 3)
                        int resStream = WlanSetInterface(handle, ref guid, wlan_intf_opcode_media_streaming_mode, sizeof(int), pBoolTrue, IntPtr.Zero);

                        if (resScan == 0)
                        {
                            _optimizedGuids.Add(guid);
                            result.AffectedInterfaces.Add(desc);
                            result.Logs.Add($"[+] Wi-Fi '{desc}': background scan disabled, minimal jitter streaming mode active.");
                        }
                        else
                        {
                            allOk = false;
                            result.Logs.Add($"[-] Error for '{desc}': WlanSetInterface code {resScan}");
                        }
                    }
                    else
                    {
                        // Restore default parameters without dropping connection
                        WlanSetInterface(handle, ref guid, wlan_intf_opcode_background_scan_enabled, sizeof(int), pBoolTrue, IntPtr.Zero);
                        WlanSetInterface(handle, ref guid, wlan_intf_opcode_media_streaming_mode, sizeof(int), pBoolFalse, IntPtr.Zero);

                        _optimizedGuids.Remove(guid);
                        result.AffectedInterfaces.Add(desc);
                        result.Logs.Add($"[+] Wi-Fi '{desc}': standard network scan restored.");
                    }
                }

                IsGamingModeActive = enableGamingMode && allOk;
                result.Success = allOk;
                result.IsGamingModeActive = IsGamingModeActive;
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(pBoolFalse);
                Marshal.FreeHGlobal(pBoolTrue);
            }
        }
    }

    /// <summary>
    /// Restores default background scanning on shutdown without interrupting active Wi-Fi connection.
    /// </summary>
    public static void RestoreDefaultScan()
    {
        lock (_lock)
        {
            if (_wlanHandle != IntPtr.Zero)
            {
                try
                {
                    IntPtr pBoolTrue = Marshal.AllocHGlobal(sizeof(int));
                    IntPtr pBoolFalse = Marshal.AllocHGlobal(sizeof(int));
                    Marshal.WriteInt32(pBoolTrue, 1);
                    Marshal.WriteInt32(pBoolFalse, 0);

                    var ifaces = GetNativeInterfaces(_wlanHandle);
                    foreach (var iface in ifaces)
                    {
                        var guid = iface.InterfaceGuid;
                        // Restore scan and disable streaming mode without disconnecting
                        WlanSetInterface(_wlanHandle, ref guid, wlan_intf_opcode_background_scan_enabled, sizeof(int), pBoolTrue, IntPtr.Zero);
                        WlanSetInterface(_wlanHandle, ref guid, wlan_intf_opcode_media_streaming_mode, sizeof(int), pBoolFalse, IntPtr.Zero);
                    }

                    Marshal.FreeHGlobal(pBoolTrue);
                    Marshal.FreeHGlobal(pBoolFalse);

                    WlanCloseHandle(_wlanHandle, IntPtr.Zero);
                }
                catch { }
                finally
                {
                    _wlanHandle = IntPtr.Zero;
                    _optimizedGuids.Clear();
                    IsGamingModeActive = false;
                }
            }
        }
    }

    private static WlanOptimizationResult SetGamingModeFallback(bool enable)
    {
        var result = new WlanOptimizationResult();
        string val = enable ? "no" : "yes";
        try
        {
            string? interfaceName = null;
            try
            {
                var psiQuery = new ProcessStartInfo
                {
                    FileName = "netsh.exe",
                    Arguments = "wlan show interfaces",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var procQuery = Process.Start(psiQuery);
                if (procQuery != null)
                {
                    string output = procQuery.StandardOutput.ReadToEnd();
                    procQuery.WaitForExit(2000);
                    var names = WlanInterfaceParser.ParseInterfaceNames(output);
                    interfaceName = names.FirstOrDefault();
                }
            }
            catch { }

            interfaceName ??= NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)?.Name;

            if (string.IsNullOrEmpty(interfaceName))
            {
                result.Success = false;
                result.Logs.Add("[-] Беспроводные интерфейсы Wi-Fi не обнаружены.");
                return result;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "netsh.exe",
                Arguments = $"wlan set autoconfig enabled={val} interface=\"{interfaceName}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(2000);
            result.Success = proc?.ExitCode == 0;
            if (result.Success)
            {
                result.AffectedInterfaces.Add(interfaceName);
            }
            IsGamingModeActive = enable && result.Success;
        }
        catch { }
        return result;
    }
}