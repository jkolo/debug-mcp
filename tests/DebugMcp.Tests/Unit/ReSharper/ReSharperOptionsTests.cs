using DebugMcp.Services.ReSharper;

namespace DebugMcp.Tests.Unit.ReSharper;

public sealed class ReSharperOptionsTests
{
    private static void WithEnv(string name, string? value, Action body)
    {
        var prev = Environment.GetEnvironmentVariable(name);
        try
        {
            Environment.SetEnvironmentVariable(name, value);
            body();
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, prev);
        }
    }

    [Fact]
    public void Create_Defaults_AreEnabledWithPinnedVersionAndTimeouts()
    {
        var o = ReSharperOptions.Create();

        o.Enabled.Should().BeTrue();
        o.Version.Should().Be(ReSharperOptions.DefaultVersion);
        o.AcquisitionTimeoutSeconds.Should().Be(600);
        o.InspectionTimeoutSeconds.Should().Be(300);
        o.MaxResults.Should().Be(500);
        o.CacheDirectory.Should().Be(ReSharperOptions.DefaultCacheDirectory);
        o.EngineToolPath.Should().Be(Path.Combine(ReSharperOptions.DefaultCacheDirectory, ReSharperOptions.DefaultVersion));
    }

    [Fact]
    public void Create_NoResharperFlag_DisablesIntegration()
    {
        ReSharperOptions.Create(noResharper: true).Enabled.Should().BeFalse();
    }

    [Fact]
    public void Create_CliOverridesEnv_ForCacheAndVersion()
    {
        WithEnv("DEBUG_MCP_RESHARPER_CACHE", "/env/cache", () =>
        WithEnv("DEBUG_MCP_RESHARPER_VERSION", "1.0.0-env", () =>
        {
            var o = ReSharperOptions.Create(resharperCache: "/cli/cache", resharperVersion: "9.9.9-cli");
            o.CacheDirectory.Should().Be("/cli/cache");
            o.Version.Should().Be("9.9.9-cli");
        }));
    }

    [Fact]
    public void Create_EnvUsedWhenNoCliArg()
    {
        WithEnv("DEBUG_MCP_RESHARPER_VERSION", "2025.1.0", () =>
        {
            ReSharperOptions.Create().Version.Should().Be("2025.1.0");
        });
    }

    [Fact]
    public void Create_EnvNoResharper_DisablesIntegration()
    {
        WithEnv("DEBUG_MCP_NO_RESHARPER", "true", () =>
        {
            ReSharperOptions.Create().Enabled.Should().BeFalse();
        });
    }

    [Fact]
    public void Create_EnvTimeoutsAndMax_AreApplied()
    {
        WithEnv("DEBUG_MCP_RESHARPER_ACQUIRE_TIMEOUT", "900", () =>
        WithEnv("DEBUG_MCP_RESHARPER_INSPECT_TIMEOUT", "120", () =>
        WithEnv("DEBUG_MCP_RESHARPER_MAX_RESULTS", "50", () =>
        {
            var o = ReSharperOptions.Create();
            o.AcquisitionTimeoutSeconds.Should().Be(900);
            o.InspectionTimeoutSeconds.Should().Be(120);
            o.MaxResults.Should().Be(50);
        })));
    }

    [Fact]
    public void Create_ExpandsTildeInCachePath()
    {
        var o = ReSharperOptions.Create(resharperCache: "~/custom-rs");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        o.CacheDirectory.Should().Be(Path.Combine(home, "custom-rs"));
    }
}
