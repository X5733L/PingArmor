using Microsoft.Win32;
using PingArmor.Services;
using Xunit;

namespace PingArmor.Tests;

public class DnsHelperTests
{
    private const string SmartDnsKey = @"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient";
    private const string WpadKey = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    [Fact]
    public void ConfigureSmartDnsPolicy_Enabled_WritesDwordToOne()
    {
        var registry = new InMemoryRegistryAccessor();

        DnsHelper.ConfigureSmartDnsPolicy(true, registry);

        Assert.Equal(1, registry.ReadDword(RegistryHive.LocalMachine, SmartDnsKey, "DisableSmartNameResolution"));
    }

    [Fact]
    public void ConfigureSmartDnsPolicy_Disabled_DeletesValue()
    {
        var registry = new InMemoryRegistryAccessor();
        registry.WriteDword(RegistryHive.LocalMachine, SmartDnsKey, "DisableSmartNameResolution", 1);

        DnsHelper.ConfigureSmartDnsPolicy(false, registry);

        Assert.Null(registry.ReadDword(RegistryHive.LocalMachine, SmartDnsKey, "DisableSmartNameResolution"));
    }

    [Fact]
    public void ConfigureWpadPolicy_Disabled_WritesAutoDetectZero()
    {
        var registry = new InMemoryRegistryAccessor();

        DnsHelper.ConfigureWpadPolicy(true, registry);

        Assert.Equal(0, registry.ReadDword(RegistryHive.CurrentUser, WpadKey, "AutoDetect"));
    }

    [Fact]
    public void ConfigureWpadPolicy_Enabled_WritesAutoDetectOne()
    {
        var registry = new InMemoryRegistryAccessor();

        DnsHelper.ConfigureWpadPolicy(false, registry);

        Assert.Equal(1, registry.ReadDword(RegistryHive.CurrentUser, WpadKey, "AutoDetect"));
    }

    [Fact]
    public void GetSmartDnsStatusDescription_ReflectsWrittenValue()
    {
        var registry = new InMemoryRegistryAccessor();

        DnsHelper.ConfigureSmartDnsPolicy(true, registry);
        string status = DnsHelper.GetSmartDnsStatusDescription(registry);

        Assert.Contains("DISABLED", status);
    }
}
