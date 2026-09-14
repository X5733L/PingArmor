using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using PingArmor.Config;
using PingArmor.Localization;
using PingArmor.Services;
using PingArmor.UI;

namespace PingArmor;

public static class Program
{
    private const string MutexName = @"Global\PingArmor_SingleInstance_Mutex";

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_HIDE = 0;

    public static bool IsAdministrator()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    public static bool TrySelfElevate(string[] args)
    {
        if (IsAdministrator()) return true;

        try
        {
            var exePath = Environment.ProcessPath ?? Application.ExecutablePath;
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = string.Join(" ", args),
                Verb = "runas",
                UseShellExecute = true
            };
            var proc = Process.Start(psi);
            if (proc != null)
            {
                return false; // Re-launched with elevated privileges
            }
        }
        catch
        {
            // User declined UAC prompt
        }

        return true;
    }

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch { }

        var config = AppConfig.Load();
        LocalizationService.SetLanguage(config.Language);

        // Process --lang / -l argument
        string[] cleanArgs = ProcessLanguageArg(args, config);

        // For operations requiring administrative privileges, request elevation during interactive launch
        if (cleanArgs.Length == 0 || RequiresElevation(cleanArgs[0]))
        {
            if (!IsAdministrator() && Environment.UserInteractive)
            {
                if (!TrySelfElevate(args))
                {
                    return 0;
                }
            }
        }

        var engine = new NetworkEngine(config);

        if (cleanArgs.Length > 0)
        {
            return ExecuteCliCommand(cleanArgs[0], engine, config);
        }

        return RunTrayApplication(config, engine);
    }

    private static string[] ProcessLanguageArg(string[] args, AppConfig config)
    {
        var resultList = new System.Collections.Generic.List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--lang", StringComparison.OrdinalIgnoreCase) ||
                args[i].Equals("-l", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    string langCode = args[++i];
                    var parsed = AppLanguageExtensions.FromCode(langCode);
                    LocalizationService.SetLanguage(parsed);
                    config.Language = parsed.ToCode();
                    config.Save();
                }
            }
            else
            {
                resultList.Add(args[i]);
            }
        }
        return resultList.ToArray();
    }

    private static bool RequiresElevation(string command) =>
        command.Equals("--optimize", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("-o", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("--install-startup", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("--gaming-on", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("--gaming-off", StringComparison.OrdinalIgnoreCase);

    private static int ExecuteCliCommand(string command, NetworkEngine engine, AppConfig config)
    {
        var s = LocalizationService.Strings;
        switch (command.ToLowerInvariant())
        {
            case "--optimize":
            case "-o":
                Console.WriteLine(s.CliOptimizing);
                var adapters = engine.GetAdapters();
                var plan = MetricDecisionEngine.Evaluate(adapters, config);
                Console.WriteLine(plan.Summary);
                var result = engine.ApplyPlan(plan);
                foreach (var log in result.Logs)
                {
                    Console.WriteLine(log);
                }
                return result.Success ? 0 : 1;

            case "--status":
            case "-s":
                Console.WriteLine(s.CliStatusHeader);
                var list = engine.GetAdapters();
                foreach (var a in list)
                {
                    Console.WriteLine($"[{a.InterfaceIndex,2}] {a.Name,-30} | {a.Type,-16} | Up: {a.IsUp,-5} | Inet: {a.HasInternet,-5} | Metric: {a.CurrentIPv4Metric}");
                }
                var p = MetricDecisionEngine.Evaluate(list, config);
                Console.WriteLine();
                Console.WriteLine(string.Format(s.CliAnalysisResultFormat, p.Summary));
                return 0;

            case "--install-startup":
                bool installed = StartupManager.EnableStartup();
                Console.WriteLine(installed ? s.StartupTaskSuccess : s.StartupTaskFail);
                return installed ? 0 : 1;

            case "--gaming-on":
                Console.WriteLine(s.CliGamingOn);
                var resOn = WlanOptimizerService.SetGamingMode(true);
                foreach (var l in resOn.Logs) Console.WriteLine(l);
                return resOn.Success ? 0 : 1;

            case "--gaming-off":
                Console.WriteLine(s.CliGamingOff);
                var resOff = WlanOptimizerService.SetGamingMode(false);
                foreach (var l in resOff.Logs) Console.WriteLine(l);
                return resOff.Success ? 0 : 1;

            case "--uninstall-startup":
                bool removed = StartupManager.DisableStartup();
                Console.WriteLine(removed ? s.StartupTaskRemoveSuccess : s.StartupTaskRemoveFail);
                return removed ? 0 : 1;

            case "--help":
            case "-h":
                PrintHelp(s);
                return 0;

            default:
                PrintHelp(s);
                return 1;
        }
    }

    private static void PrintHelp(LocalizedStrings s)
    {
        Console.WriteLine(s.AppTitle);
        Console.WriteLine(s.CliUsage);
        Console.WriteLine($"  PingArmor.exe                     {s.CliHelpRunTray}");
        Console.WriteLine($"  PingArmor.exe --optimize (-o)     {s.CliHelpOptimize}");
        Console.WriteLine($"  PingArmor.exe --status (-s)       {s.CliHelpStatus}");
        Console.WriteLine($"  PingArmor.exe --install-startup   {s.CliHelpInstallStartup}");
        Console.WriteLine($"  PingArmor.exe --uninstall-startup {s.CliHelpUninstallStartup}");
        Console.WriteLine($"  PingArmor.exe --gaming-on         {s.CliHelpGamingOn}");
        Console.WriteLine($"  PingArmor.exe --gaming-off        {s.CliHelpGamingOff}");
        Console.WriteLine($"  PingArmor.exe --lang <ru|en|kk>   {s.CliHelpLang}");
    }

    private static int RunTrayApplication(AppConfig config, NetworkEngine engine)
    {
        var s = LocalizationService.Strings;

        // Run in System Tray mode: hide console window
        var consoleHandle = GetConsoleWindow();
        if (consoleHandle != IntPtr.Zero)
        {
            ShowWindow(consoleHandle, SW_HIDE);
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var mutex = new Mutex(true, MutexName, out bool isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show(
                s.AlreadyRunning,
                s.AppTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return 0;
        }

        using var monitor = new NetworkMonitor(engine, config);
        using var trayContext = new TrayApplicationContext(config, engine, monitor);

        Application.Run(trayContext);
        return 0;
    }
}
