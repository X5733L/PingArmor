using PingArmor.Common;
using Xunit;

namespace PingArmor.Tests;

public class AppVersionTests
{
    [Fact]
    public void AppVersion_Current_ReturnsValidNonEmptyVersion()
    {
        var version = AppVersion.Current;

        Assert.False(string.IsNullOrWhiteSpace(version));
        // Should start with digit or contain semantic version numbers
        Assert.Matches(@"^\d+\.\d+(\.\d+)?.*$", version);
    }
}
