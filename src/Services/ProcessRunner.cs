using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PingArmor.Services;

/// <summary>
/// Result of an external process invocation.
/// </summary>
public readonly record struct ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut)
{
    public bool Succeeded => !TimedOut && ExitCode == 0;

    public static ProcessResult Failure(string message) =>
        new(-1, string.Empty, message, false);

    public static ProcessResult Timeout(string output, string error) =>
        new(-1, output, error, true);
}

public interface IProcessRunner
{
    /// <summary>Synchronously runs a process and captures its output, killing it on timeout.</summary>
    ProcessResult Run(string fileName, string arguments, int timeoutMs);

    /// <summary>Asynchronously runs a process and captures its output, killing it on timeout/cancellation.</summary>
    Task<ProcessResult> RunAsync(string fileName, string arguments, int timeoutMs, CancellationToken cancellationToken = default);
}

/// <summary>
/// Safe process runner that fully drains stdout/stderr to avoid pipe-buffer deadlocks
/// and forcibly terminates the child process (and its tree) when it exceeds the timeout.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    public static ProcessRunner Default { get; } = new();

    public ProcessResult Run(string fileName, string arguments, int timeoutMs)
        => RunAsync(fileName, arguments, timeoutMs, CancellationToken.None).GetAwaiter().GetResult();

    public async Task<ProcessResult> RunAsync(string fileName, string arguments, int timeoutMs, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        Process? process = null;
        try
        {
            process = new Process { StartInfo = psi };
            process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

            if (!process.Start())
            {
                return ProcessResult.Failure($"Failed to start '{fileName}'.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeoutCts = new CancellationTokenSource();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            if (timeoutMs > 0)
            {
                timeoutCts.CancelAfter(timeoutMs);
            }

            try
            {
                await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                return ProcessResult.Timeout(stdout.ToString(), stderr.ToString());
            }

            // Ensure asynchronous output handlers have flushed.
            try
            {
                process.WaitForExit();
            }
            catch
            {
                // ignored
            }

            return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString(), false);
        }
        catch (Exception ex)
        {
            TryKill(process);
            return ProcessResult.Failure(ex.Message);
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static void TryKill(Process? process)
    {
        try
        {
            if (process != null && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Process already gone or access denied — nothing else we can do.
        }
    }
}
