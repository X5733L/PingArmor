using System;
using PingArmor.Config;
using PingArmor.Services;

namespace PingArmor.UI;

/// <summary>
/// Minimal composition root for the UI layer. Values are captured once at startup
/// so XAML-declared views can create their view models without a DI container.
/// </summary>
public static class AppServices
{
    public static AppConfig? Config { get; private set; }
    public static INetworkEngine? Engine { get; private set; }
    public static NetworkMonitor? Monitor { get; private set; }
    public static Action<string> LogAppender { get; private set; } = _ => { };

    public static bool IsReady => Config is not null && Engine is not null && Monitor is not null;

    public static void Initialize(AppConfig config, INetworkEngine engine, NetworkMonitor monitor, Action<string> logAppender)
    {
        Config = config;
        Engine = engine;
        Monitor = monitor;
        LogAppender = logAppender ?? (_ => { });
    }
}