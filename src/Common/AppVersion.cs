using System;
using System.Reflection;

namespace PingArmor.Common;

public static class AppVersion
{
    private static string? _version;

    public static string Current => _version ??= ResolveVersion();

    private static string ResolveVersion()
    {
        var asm = typeof(AppVersion).Assembly;

        var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoVer))
        {
            // InformationalVersion may contain commit hash or metadata, e.g. "1.0.3+commit_sha"
            string clean = infoVer.Split('+')[0].Trim();
            if (clean.StartsWith('v') || clean.StartsWith('V'))
            {
                clean = clean[1..];
            }
            if (!string.IsNullOrWhiteSpace(clean))
            {
                return clean;
            }
        }

        var ver = asm.GetName().Version;
        if (ver != null)
        {
            return ver.Build > 0
                ? $"{ver.Major}.{ver.Minor}.{ver.Build}"
                : $"{ver.Major}.{ver.Minor}";
        }

        return "1.0.0";
    }
}
