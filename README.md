# PingArmor 🛡️

[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011%20(x64)-blue.svg)](https://microsoft.com)
[![Runtime](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

**PingArmor** is a lightweight, zero-overhead background utility for Windows that keeps your primary internet connection prioritized, eliminates Wi-Fi latency spikes, and protects against VPN route hijacking and DNS leaks.

---

### 🌐 Language

**English** • [Русский](README.ru.md) • [Қазақша](README.kk.md)

---

## 💡 Why PingArmor?

If you frequently use VPNs (WireGuard, OpenVPN, Fortinet, Sing-box, Tailscale, v2ray) or rely on Wi-Fi for gaming and remote work, you have likely run into these Windows quirks:

1. **VPN Route Hijacking**: Many VPN clients override the system interface metric to `1`. Suddenly, all local traffic gets forced into the tunnel, disconnecting your IDE (Antigravity, VS Code, JetBrains), breaking local servers, and stalling Electron apps.
2. **Wi-Fi Ping Spikes & Jitter**: Every 60 seconds, Windows initiates an aggressive background scan for nearby Wi-Fi access points. If you are playing an online game (CS2, Valorant, Apex) or on a Discord / Zoom call, this causes sudden 100–300ms lag spikes.
3. **DNS Delays & Multi-Homed Leaks**: Windows often probes multiple network interfaces simultaneously (Smart Multi-Homed Name Resolution), which causes slow lookups, connection timeouts, and DNS leaks.

**PingArmor solves these issues automatically and silently in the background.**

---

## ✨ Key Features

- ⚡ **Intelligent Interface Priority**:
  - Automatically identifies physical Ethernet and Wi-Fi adapters with verified internet connectivity (via NCSI).
  - Assigns **metric 5** to wired Ethernet (highest priority) and **metric 10** to Wi-Fi.
  - Automatically moves VPN, TAP, Wintun, and virtual adapters down to **metric 500** if they attempt to hijack the default route.
- 📶 **Wi-Fi Background Scan Suppressor (Anti-Lag)**:
  - Uses the native Win32 WLAN API (`wlanapi.dll` OpCode 2 & 3) to smoothly turn off background scans and enable media streaming mode.
  - **Zero connection drops or reconnects** — your active connection stays rock solid, eliminating micro-stutters and jitter.
  - *Smart Connect-First*: waits until your Wi-Fi is connected before suppressing scans so your PC connects to routers normally at startup.
- 🔒 **DNS Leak & WPAD Optimization**:
  - Sets `DisableSmartNameResolution = 1` in registry to eliminate multi-homed resolution delays and leaks.
  - Disables WPAD auto-proxy discovery to remove connection startup pauses.
  - Flushes the system DNS resolver cache via native `DnsFlushResolverCache` without intrusive popping terminal windows.
- 🪶 **Whisper-Quiet Background Tray**:
  - Runs in the Windows system tray with a dynamic shield icon (🟢 Protected, 🟡 Adjusting, 🔴 No connection, ⚪ Paused).
  - Uses event-driven monitoring (`NetworkChange.NetworkAddressChanged`) with a 750ms debounce filter — **0.0% CPU usage and ~15 MB RAM**.
- 🚀 **Silent UAC-Free Startup**:
  - Integrated Task Scheduler installer registers a logon task with highest privileges, launching with administrator rights without annoying UAC prompts.
- 🌐 **Multilingual by Design**:
  - Full UI and CLI localization for English, Russian, and Kazakh.

---

## 🚀 Quick Start

### 1. Download Pre-Built Binary
Download the latest ready-to-run release from **[GitHub Releases](../../releases)**:
- **`PingArmor-v*-win-x64.zip`**: Fully portable standalone single-file executable (~50 MB). All .NET 10 runtimes are embedded. Works out-of-the-box on any Windows 10/11 PC without installing any .NET runtimes or SDKs.

Just extract the ZIP and run `PingArmor.exe`!

### 2. Run Directly from Source
If you have [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) installed:
```powershell
# Clone and launch immediately
git clone https://github.com/X5733L/PingArmor.git
cd PingArmor
dotnet run --project src/PingArmor.csproj
```

### 3. Local Single-File Build
To compile a portable standalone executable locally:
```powershell
dotnet publish src/PingArmor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./dist/publish
.\dist\publish\PingArmor.exe
```

---

## 🖥️ System Tray Menu

Right-clicking the tray shield icon gives you instant control:

| Menu Item | Description |
| :--- | :--- |
| **Status / Primary Channel** | Shows the active internet adapter and its current metric |
| **⚡ Optimize network priorities** | Manually evaluates all adapters, restores metrics, and flushes DNS |
| **⏸ Pause protection** | Temporarily pauses automatic event monitoring |
| **🚀 Launch on Windows startup** | Toggles silent logon task via Windows Task Scheduler (no UAC dialogs) |
| **📶 Disable Wi-Fi background scan** | Toggles background Wi-Fi scan suppression for lag-free gaming/calls |
| **🔔 Notifications** | Enables or disables tray notifications on changes |
| **🌐 Language** | Switch between English, Русский, and Қазақша on the fly |
| **📋 Event log** | Opens real-time diagnostic log window |
| **❌ Exit** | Restores default network parameters and cleanly exits |

---

## 💻 CLI Commands

PingArmor can also be used as a headless command-line tool in scripts or automations:

```powershell
# Launch in background tray mode
PingArmor.exe

# View all adapters, types, and current metrics
PingArmor.exe --status

# One-off network optimization
PingArmor.exe --optimize

# Enable / disable Wi-Fi background scan suppression
PingArmor.exe --gaming-on
PingArmor.exe --gaming-off

# Register / unregister silent startup in Windows Task Scheduler
PingArmor.exe --install-startup
PingArmor.exe --uninstall-startup

# Set application language
PingArmor.exe --lang en    # English
PingArmor.exe --lang ru    # Russian
PingArmor.exe --lang kk    # Kazakh
```

---

## ⚙️ Configuration (`config.json`)

On startup, `config.json` is automatically loaded or created beside the executable:

```json
{
  "CheckIntervalSeconds": 10,
  "PrimaryEthernetMetric": 5,
  "PrimaryWifiMetric": 10,
  "VirtualAdapterMetric": 500,
  "DisconnectedAdapterMetric": 100,
  "DisableSmartNameResolution": true,
  "DisableWpad": true,
  "FlushDnsOnChange": true,
  "ShowNotifications": true,
  "DisableIPv6OnWifi": true,
  "EnableWlanOptimizer": true,
  "Language": "en",
  "ExcludeAdapters": []
}
```

### Options Overview:
- `CheckIntervalSeconds`: Watchdog heartbeat timer interval (seconds) for quiet external changes.
- `PrimaryEthernetMetric`: Metric applied to primary active Ethernet (default: `5`).
- `PrimaryWifiMetric`: Metric applied to primary active Wi-Fi (default: `10`).
- `VirtualAdapterMetric`: Metric applied to VPN / TAP / virtual adapters (default: `500`).
- `DisconnectedAdapterMetric`: Metric applied to unplugged physical adapters (default: `100`).
- `EnableWlanOptimizer`: Toggles native Wi-Fi background scan suppression (default: `true`).
- `ExcludeAdapters`: List of adapter names or descriptions to ignore during optimization.

---

## 🛠️ Building from Source

### Prerequisites
- Windows 10 (version 1809+) or Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Build Commands
```powershell
# Clone repository
git clone https://github.com/X5733L/PingArmor.git
cd PingArmor

# Build solution
dotnet build

# Publish standalone single-file binary (no runtime required)
dotnet publish src/PingArmor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./dist/publish
```

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
