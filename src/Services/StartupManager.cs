using System;
using System.Diagnostics;
using System.IO;

namespace PingArmor.Services;

public static class StartupManager
{
    public const string TaskName = "PingArmor";

    public static bool IsStartupEnabled() =>
        RunSchtasks($"/query /tn \"{TaskName}\"", 2000);

    public static bool EnableStartup(string? customExePath = null)
    {
        string exePath = customExePath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
        {
            return false;
        }

        return RunSchtasks($"/create /tn \"{TaskName}\" /tr \"\\\"{exePath}\\\"\" /sc onlogon /rl highest /f", 3000);
    }

    public static bool DisableStartup() =>
        RunSchtasks($"/delete /tn \"{TaskName}\" /f", 3000);

    private static bool RunSchtasks(string arguments, int timeoutMs)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(timeoutMs);
            return p?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
