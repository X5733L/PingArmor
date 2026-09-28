using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using PingArmor.Services;
using Xunit;

namespace PingArmor.Tests;

public class ProcessRunnerTests
{
    [Fact]
    public void Run_CapturesStandardOutputAndExitCode()
    {
        var runner = new ProcessRunner();

        var result = runner.Run("cmd.exe", "/c echo pingarmor-test", 10000);

        Assert.False(result.TimedOut);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("pingarmor-test", result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_KillsProcessOnTimeout()
    {
        var runner = new ProcessRunner();
        var sw = Stopwatch.StartNew();

        var result = await runner.RunAsync("cmd.exe", "/c ping -n 10 127.0.0.1 >nul", 400);

        sw.Stop();
        Assert.True(result.TimedOut);
        // Must not have waited for the full ping duration (~9s).
        Assert.True(sw.ElapsedMilliseconds < 5000, $"Timeout took {sw.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Run_NonExistentExecutable_ReturnsFailureWithoutThrowing()
    {
        var runner = new ProcessRunner();

        var result = runner.Run("definitely-not-an-executable-12345.exe", "", 2000);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RunAsync_IsCancellable()
    {
        var runner = new ProcessRunner();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(200);

        var result = await runner.RunAsync("cmd.exe", "/c ping -n 10 127.0.0.1 >nul", 10000, cts.Token);

        Assert.True(result.TimedOut);
    }
}
