namespace PingArmor.Tests;

/// <summary>
/// Shared locks for tests that mutate process-wide state (e.g. the localization service),
/// so test classes running in parallel cannot interfere with each other.
/// </summary>
internal static class TestSync
{
    public static readonly object Localization = new();
}
