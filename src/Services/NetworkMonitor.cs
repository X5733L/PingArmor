using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using PingArmor.Config;
using PingArmor.Models;

namespace PingArmor.Services;

public class NetworkMonitor : IDisposable
{
    private readonly INetworkEngine _engine;
    private readonly AppConfig _config;
    private readonly System.Threading.Timer _watchdogTimer;
    private readonly System.Threading.Timer _debounceTimer;
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly object _lock = new();

    private CancellationTokenSource? _cts;
    private Task? _worker;

    private bool _isRunning;
    private bool _isEvaluating;
    private bool _recheckRequested;
    private bool _manualCheckRequested;
    private bool _pending;

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
            _isEvaluating = false;
            _pending = false;
            _recheckRequested = false;

            _cts = new CancellationTokenSource();
            _worker = Task.Run(() => WorkerLoopAsync(_cts.Token));
        }

        try
        {
            NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        }
        catch (Exception ex)
        {
            RaiseLog($"[-] Failed to subscribe to network events: {ex.Message}");
        }

        int intervalMs = Math.Max(3, _config.CheckIntervalSeconds) * 1000;
        _watchdogTimer.Change(intervalMs, intervalMs);

        RaiseLog($"[+] Protection enabled. Background monitoring started (check interval: {_config.CheckIntervalSeconds}s).");
        Raise(StatusChanged, true);

        // Ensure system settings backup exists before first evaluation
        try
        {
            if (BackupService.CreateBackupIfNotExists())
            {
                RaiseLog("[+] System settings backup created (backups/backup.json). Use --restore to revert changes.");
            }
        }
        catch (Exception ex)
        {
            RaiseLog($"[!] Warning: failed to create settings backup: {ex.Message}");
        }

        // Initial check immediately
        ScheduleEvaluation(100);
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        Task? worker;

        lock (_lock)
        {
            if (!_isRunning) return;
            _isRunning = false;
            _isEvaluating = false;
            cts = _cts;
            worker = _worker;
            _cts = null;
            _worker = null;
        }

        try
        {
            NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        }
        catch { }

        _watchdogTimer.Change(Timeout.Infinite, Timeout.Infinite);
        _debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);

        // Cancel the in-flight evaluation and unblock the worker.
        try { cts?.Cancel(); } catch { }
        Wake();

        try
        {
            worker?.Wait(3000);
        }
        catch { }

        cts?.Dispose();

        RaiseLog("[*] Protection paused. Background monitoring stopped.");
        Raise(StatusChanged, false);
    }

    public void TriggerManualCheck()
    {
        RaiseLog("[*] Manual network optimization requested...");
        lock (_lock)
        {
            _manualCheckRequested = true;
        }
        ScheduleEvaluation(0);
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        RaiseLog("[~] Network address change detected (NetworkAddressChanged).");
        ScheduleEvaluation(750);
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        RaiseLog($"[~] Network availability change detected (available: {e.IsAvailable}).");
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
            _pending = true;
        }
        Wake();
    }

    private void Wake()
    {
        try
        {
            if (_wake.CurrentCount == 0)
            {
                _wake.Release();
            }
        }
        catch (SemaphoreFullException) { }
        catch (ObjectDisposedException) { }
    }

    private async Task WorkerLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await _wake.WaitAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            if (token.IsCancellationRequested) return;

            // Drain any queued permits so requests are coalesced.
            while (_wake.Wait(0)) { }

            while (!token.IsCancellationRequested)
            {
                bool isManual;
                lock (_lock)
                {
                    _pending = false;
                    isManual = _manualCheckRequested;
                    _manualCheckRequested = false;
                    _recheckRequested = false;
                    _isEvaluating = true;
                }

                try
                {
                    ExecuteCheck(isManual, token);
                }
                catch (OperationCanceledException)
                {
                    lock (_lock) { _isEvaluating = false; }
                    return;
                }
                catch (Exception ex)
                {
                    RaiseLog($"[-] Error during network evaluation: {ex.Message}");
                }

                lock (_lock)
                {
                    if (!_recheckRequested && !_pending && !_manualCheckRequested)
                    {
                        _isEvaluating = false;
                        break;
                    }
                }
            }
        }
    }

    public OptimizationPlan ExecuteCheck(bool isManual = false)
        => ExecuteCheck(isManual, CancellationToken.None);

    private OptimizationPlan ExecuteCheck(bool isManual, CancellationToken token)
    {
        var adapters = _engine.GetAdapters(token);
        var plan = MetricDecisionEngine.Evaluate(adapters, _config);

        Raise(PlanEvaluated, plan);

        foreach (var warning in plan.Warnings)
        {
            RaiseLog($"[!] {warning}");
        }

        if (plan.NeedsOptimization)
        {
            RaiseLog($"[!] {plan.Summary}");
            var result = _engine.ApplyPlan(plan, force: false, cancellationToken: token);
            LogResult(result);
            Raise(OptimizationApplied, result);
        }
        else if (isManual)
        {
            RaiseLog($"[*] {plan.Summary}");
            var result = _engine.ApplyPlan(plan, force: true, cancellationToken: token);
            LogResult(result);
            Raise(OptimizationApplied, result);
        }

        // Automatically enable Wi-Fi optimization once connected to access point (Smart Connect-First).
        // Only (re)apply when it is not already active, or when the user explicitly asked for it.
        if (_config.EnableWlanOptimizer)
        {
            try
            {
                if (WlanOptimizerService.IsAnyWifiConnected())
                {
                    bool wasActive = WlanOptimizerService.IsGamingModeActive;
                    if (!wasActive || isManual)
                    {
                        var wlanRes = WlanOptimizerService.SetGamingMode(true);
                        foreach (var log in wlanRes.Logs)
                        {
                            RaiseLog(log);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RaiseLog($"[-] Wi-Fi gaming mode activation error: {ex.Message}");
            }
        }

        return plan;
    }

    private void LogResult(OptimizationResult result)
    {
        foreach (var log in result.Logs)
        {
            RaiseLog(log);
        }

        if (!result.Success && !string.IsNullOrEmpty(result.Error))
        {
            RaiseLog($"[-] Optimization completed with errors: {result.Error}");
        }
    }

    private void RaiseLog(string message) => Raise(LogMessage, message);

    private void Raise<T>(Action<T>? handler, T argument)
    {
        if (handler == null) return;
        foreach (var subscriber in handler.GetInvocationList())
        {
            try
            {
                ((Action<T>)subscriber)(argument);
            }
            catch
            {
                // A faulty subscriber must not abort the evaluation pipeline.
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _watchdogTimer.Dispose();
        _debounceTimer.Dispose();
        _wake.Dispose();
    }
}
