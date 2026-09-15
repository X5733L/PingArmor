using System;
using System.Net.NetworkInformation;
using System.Threading;
using PingArmor.Config;
using PingArmor.Models;

namespace PingArmor.Services;

public class NetworkMonitor : IDisposable
{
    private readonly INetworkEngine _engine;
    private readonly AppConfig _config;
    private readonly System.Threading.Timer _watchdogTimer;
    private readonly System.Threading.Timer _debounceTimer;
    private readonly object _lock = new();

    private bool _isRunning;
    private bool _isEvaluating;
    private bool _recheckRequested;
    private bool _manualCheckRequested;

    public event Action<OptimizationPlan>? PlanEvaluated;
    public event Action<OptimizationResult>? OptimizationApplied;
    public event Action<string>? LogMessage;
    public event Action<bool>? StatusChanged;

    public bool IsRunning => _isRunning;

    public NetworkMonitor(INetworkEngine engine, AppConfig config)
    {
        _engine = engine;
        _config = config;

        _debounceTimer = new System.Threading.Timer(DebounceCallback, null, Timeout.Infinite, Timeout.Infinite);
        _watchdogTimer = new System.Threading.Timer(WatchdogCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_isRunning) return;
            _isRunning = true;

            try
            {
                NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
                NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"[-] Failed to subscribe to network events: {ex.Message}");
            }

            int intervalMs = Math.Max(3, _config.CheckIntervalSeconds) * 1000;
            _watchdogTimer.Change(intervalMs, intervalMs);

            LogMessage?.Invoke($"[+] Background monitoring started (check interval: {_config.CheckIntervalSeconds}s).");
            StatusChanged?.Invoke(true);

            // Ensure system settings backup exists before first evaluation
            try
            {
                if (BackupService.CreateBackupIfNotExists())
                {
                    LogMessage?.Invoke("[+] System settings backup created (backup.json). Use --restore to revert changes.");
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"[!] Warning: failed to create settings backup: {ex.Message}");
            }

            // Initial check immediately
            ScheduleEvaluation(100);
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning) return;
            _isRunning = false;

            try
            {
                NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
                NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
            }
            catch { }

            _watchdogTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);

            LogMessage?.Invoke("[*] Monitoring paused.");
            StatusChanged?.Invoke(false);
        }
    }

    public void TriggerManualCheck()
    {
        LogMessage?.Invoke("[*] Manual network optimization requested...");
        lock (_lock)
        {
            _manualCheckRequested = true;
        }
        ScheduleEvaluation(0);
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        LogMessage?.Invoke("[~] Network address change detected (NetworkAddressChanged).");
        ScheduleEvaluation(750);
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        LogMessage?.Invoke($"[~] Network availability change detected (available: {e.IsAvailable}).");
        ScheduleEvaluation(750);
    }

    private void WatchdogCallback(object? state)
    {
        if (!_isRunning) return;
        ScheduleEvaluation(0);
    }

    public void ScheduleEvaluation(int delayMs)
    {
        if (!_isRunning && delayMs > 0) return;
        _debounceTimer.Change(delayMs, Timeout.Infinite);
    }

    private void DebounceCallback(object? state)
    {
        lock (_lock)
        {
            if (_isEvaluating)
            {
                _recheckRequested = true;
                return;
            }
            _isEvaluating = true;
        }

        try
        {
            while (true)
            {
                bool isManual;
                lock (_lock)
                {
                    isManual = _manualCheckRequested;
                    _manualCheckRequested = false;
                    _recheckRequested = false;
                }

                ExecuteCheck(isManual);

                lock (_lock)
                {
                    if (!_recheckRequested && !_manualCheckRequested)
                    {
                        _isEvaluating = false;
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"[-] Error during network evaluation: {ex.Message}");
            lock (_lock)
            {
                _isEvaluating = false;
            }
        }
    }

    public OptimizationPlan ExecuteCheck(bool isManual = false)
    {
        var adapters = _engine.GetAdapters();
        var plan = MetricDecisionEngine.Evaluate(adapters, _config);

        PlanEvaluated?.Invoke(plan);

        if (plan.NeedsOptimization)
        {
            LogMessage?.Invoke($"[!] {plan.Summary}");
            var result = _engine.ApplyPlan(plan);
            foreach (var log in result.Logs)
            {
                LogMessage?.Invoke(log);
            }
            OptimizationApplied?.Invoke(result);
        }
        else if (isManual)
        {
            LogMessage?.Invoke($"[*] {plan.Summary}");
            var result = _engine.ApplyPlan(plan, force: true);
            foreach (var log in result.Logs)
            {
                LogMessage?.Invoke(log);
            }
            OptimizationApplied?.Invoke(result);
        }

        // Automatically enable Wi-Fi optimization once connected to access point (Smart Connect-First)
        if (_config.EnableWlanOptimizer)
        {
            try
            {
                if (WlanOptimizerService.IsAnyWifiConnected())
                {
                    bool wasActive = WlanOptimizerService.IsGamingModeActive;
                    var wlanRes = WlanOptimizerService.SetGamingMode(true);
                    if (isManual || !wasActive)
                    {
                        foreach (var log in wlanRes.Logs)
                        {
                            LogMessage?.Invoke(log);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"[-] Wi-Fi gaming mode activation error: {ex.Message}");
            }
        }

        return plan;
    }

    public void Dispose()
    {
        Stop();
        _watchdogTimer.Dispose();
        _debounceTimer.Dispose();
    }
}
