using System.Threading.Tasks;
using PingArmor.Config;
using Xunit;

namespace PingArmor.Tests;

public class AppConfigExclusionTests
{
    [Fact]
    public void AddExclusion_IsIdempotentAndCaseInsensitive()
    {
        var config = new AppConfig();

        Assert.True(config.AddExclusion("Tailscale"));
        Assert.False(config.AddExclusion("tailscale"));
        Assert.Single(config.GetExcludeSnapshot());
    }

    [Fact]
    public void RemoveExclusion_RemovesCaseInsensitively()
    {
        var config = new AppConfig();
        config.AddExclusion("Tailscale");

        Assert.True(config.RemoveExclusion("TAILSCALE"));
        Assert.Empty(config.GetExcludeSnapshot());
    }

    [Fact]
    public void GetExcludeSnapshot_IsIsolatedFromLaterMutations()
    {
        var config = new AppConfig();
        config.AddExclusion("One");

        var snapshot = config.GetExcludeSnapshot();
        config.AddExclusion("Two");

        Assert.Single(snapshot);
        Assert.Equal(2, config.GetExcludeSnapshot().Count);
    }

    [Fact]
    public async Task AddAndRemove_Concurrently_DoesNotThrow()
    {
        var config = new AppConfig();

        var writer = Task.Run(() =>
        {
            for (int i = 0; i < 500; i++)
            {
                config.AddExclusion($"Adapter{i % 50}");
                config.RemoveExclusion($"Adapter{i % 50}");
            }
        });

        var reader = Task.Run(() =>
        {
            for (int i = 0; i < 500; i++)
            {
                _ = config.GetExcludeSnapshot();
            }
        });

        await Task.WhenAll(writer, reader);
        Assert.NotNull(config.GetExcludeSnapshot());
    }
}
