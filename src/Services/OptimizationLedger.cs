using System;
using System.Collections.Generic;
using System.Linq;

namespace PingArmor.Services;

/// <summary>
/// Tracks which adapters PingArmor has modified and how often, so the monitor can
/// detect a VPN/third-party client repeatedly overwriting the metric ("metric war")
/// and back off instead of hammering the system forever.
/// </summary>
public sealed class OptimizationLedger
{
    /// <summary>Maximum number of metric writes for one adapter inside <see cref="Window"/> before backing off.</summary>
    public const int MaxAppliesPerWindow = 6;

    /// <summary>Sliding window used for the apply counter.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    public static OptimizationLedger Default { get; } = new();

    private readonly object _lock = new();
    private readonly Dictionary<int, Entry> _entries = new();

    private sealed class Entry
    {
        public string Alias = string.Empty;
        public int TargetMetric;
        public int Count;
        public DateTime FirstUtc;
        public DateTime LastUtc;
        public bool BackedOff;
    }

    public bool ShouldBackOff(int interfaceIndex, DateTime utcNow)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(interfaceIndex, out var entry)) return false;
            if (utcNow - entry.FirstUtc > Window)
            {
                // Window expired — forgive and reset.
                _entries.Remove(interfaceIndex);
                return false;
            }
            return entry.Count >= MaxAppliesPerWindow;
        }
    }

    /// <summary>Records a successful metric write. Returns true if this pushed the adapter into back-off.</summary>
    public bool RecordApply(int interfaceIndex, string alias, int targetMetric, DateTime utcNow)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(interfaceIndex, out var entry) || utcNow - entry.FirstUtc > Window)
            {
                entry = new Entry { FirstUtc = utcNow };
                _entries[interfaceIndex] = entry;
            }

            entry.Alias = alias;
            entry.TargetMetric = targetMetric;
            entry.Count++;
            entry.LastUtc = utcNow;
            entry.BackedOff = entry.Count >= MaxAppliesPerWindow;
            return entry.BackedOff;
        }
    }

    public bool IsKnown(int interfaceIndex)
    {
        lock (_lock)
        {
            return _entries.ContainsKey(interfaceIndex);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }

    public IReadOnlyList<(int InterfaceIndex, string Alias, int TargetMetric, int Count, bool BackedOff)> Snapshot()
    {
        lock (_lock)
        {
            return _entries
                .Select(kv => (kv.Key, kv.Value.Alias, kv.Value.TargetMetric, kv.Value.Count, kv.Value.BackedOff))
                .ToList();
        }
    }
}
