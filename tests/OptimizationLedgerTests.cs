using System;
using PingArmor.Services;
using Xunit;

namespace PingArmor.Tests;

public class OptimizationLedgerTests
{
    [Fact]
    public void RecordApply_BacksOffAfterThreshold()
    {
        var ledger = new OptimizationLedger();
        var now = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

        bool backedOff = false;
        for (int i = 0; i < OptimizationLedger.MaxAppliesPerWindow; i++)
        {
            backedOff = ledger.RecordApply(25, "OpenVPN", 500, now.AddSeconds(i));
        }

        Assert.True(backedOff);
        Assert.True(ledger.ShouldBackOff(25, now.AddSeconds(OptimizationLedger.MaxAppliesPerWindow)));
    }

    [Fact]
    public void ShouldBackOff_WindowExpiryForgivesAdapter()
    {
        var ledger = new OptimizationLedger();
        var now = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < OptimizationLedger.MaxAppliesPerWindow; i++)
        {
            ledger.RecordApply(25, "OpenVPN", 500, now.AddSeconds(i));
        }

        // After the sliding window passes, the adapter is forgiven.
        Assert.False(ledger.ShouldBackOff(25, now + OptimizationLedger.Window + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void ShouldBackOff_UnknownAdapter_ReturnsFalse()
    {
        var ledger = new OptimizationLedger();
        Assert.False(ledger.ShouldBackOff(42, DateTime.UtcNow));
    }

    [Fact]
    public void Clear_ResetsAllState()
    {
        var ledger = new OptimizationLedger();
        var now = DateTime.UtcNow;
        for (int i = 0; i < OptimizationLedger.MaxAppliesPerWindow; i++)
        {
            ledger.RecordApply(7, "Ethernet", 50, now);
        }

        ledger.Clear();

        Assert.False(ledger.ShouldBackOff(7, now));
        Assert.Empty(ledger.Snapshot());
    }
}
