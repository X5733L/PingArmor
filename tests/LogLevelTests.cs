using PingArmor.Services;
using Xunit;

namespace PingArmor.Tests;

public class LogLevelTests
{
    [Theory]
    [InlineData("[+] ok", LogLevel.Info)]
    [InlineData("[*] note", LogLevel.Info)]
    [InlineData("[~] noise", LogLevel.Debug)]
    [InlineData("[!] careful", LogLevel.Warn)]
    [InlineData("[-] failure", LogLevel.Error)]
    [InlineData("plain message", LogLevel.Info)]
    [InlineData("", LogLevel.Info)]
    [InlineData(null, LogLevel.Info)]
    public void InferFromMessage_MapsConventionalPrefixes(string? message, LogLevel expected)
    {
        Assert.Equal(expected, LogLevelExtensions.InferFromMessage(message));
    }

    [Theory]
    [InlineData("debug", LogLevel.Debug)]
    [InlineData("INFO", LogLevel.Info)]
    [InlineData("Warning", LogLevel.Warn)]
    [InlineData("error", LogLevel.Error)]
    [InlineData("bogus", LogLevel.Info)]
    [InlineData(null, LogLevel.Info)]
    public void Parse_UsesFallbackForUnknown(string? value, LogLevel expected)
    {
        Assert.Equal(expected, LogLevelExtensions.Parse(value, LogLevel.Info));
    }

    [Fact]
    public void ToTag_ReturnsUppercaseNames()
    {
        Assert.Equal("DEBUG", LogLevel.Debug.ToTag());
        Assert.Equal("INFO", LogLevel.Info.ToTag());
        Assert.Equal("WARN", LogLevel.Warn.ToTag());
        Assert.Equal("ERROR", LogLevel.Error.ToTag());
    }
}
