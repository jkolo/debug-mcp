using DebugMcp.Services.Symbols;

namespace DebugMcp.Tests.Unit.Symbols;

public class SymbolServerOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new SymbolServerOptions();

        options.ServerUrls.Should().HaveCount(2);
        options.ServerUrls[0].Should().Be(SymbolServerOptions.DefaultMicrosoftServer);
        options.ServerUrls[1].Should().Be(SymbolServerOptions.DefaultNuGetServer);
        options.TimeoutSeconds.Should().Be(30);
        options.MaxFileSizeMB.Should().Be(100);
        options.MaxConcurrentDownloads.Should().Be(4);
        options.Enabled.Should().BeTrue();
        options.CacheDirectory.Should().Contain(".debug-mcp")
            .And.Contain("symbols");
    }

    [Fact]
    public void Create_WithNoSymbols_SetsEnabledFalse()
    {
        var options = SymbolServerOptions.Create(noSymbols: true);

        options.Enabled.Should().BeFalse();
        // Other defaults should still apply
        options.ServerUrls.Should().HaveCount(2);
    }

    [Fact]
    public void Create_WithCustomServers_ParsesSemicolonSeparated()
    {
        var options = SymbolServerOptions.Create(
            symbolServers: "https://custom1.example.com;https://custom2.example.com");

        options.ServerUrls.Should().HaveCount(2);
        options.ServerUrls[0].Should().Be("https://custom1.example.com");
        options.ServerUrls[1].Should().Be("https://custom2.example.com");
    }

    [Fact]
    public void Create_WithCustomCache_ExpandsTilde()
    {
        var options = SymbolServerOptions.Create(symbolCache: "~/my-symbols");

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        options.CacheDirectory.Should().StartWith(home);
        options.CacheDirectory.Should().EndWith("my-symbols");
        options.CacheDirectory.Should().NotContain("~");
    }

    [Fact]
    public void Create_WithCustomTimeout_SetsValue()
    {
        var options = SymbolServerOptions.Create(symbolTimeout: 60);

        options.TimeoutSeconds.Should().Be(60);
    }

    [Fact]
    public void Create_WithCustomMaxSize_SetsValue()
    {
        var options = SymbolServerOptions.Create(symbolMaxSize: 200);

        options.MaxFileSizeMB.Should().Be(200);
    }

    [Fact]
    public void Create_WithEnvVarServers_UsesEnvVar()
    {
        var envKey = "DEBUG_MCP_SYMBOL_SERVERS";
        var original = Environment.GetEnvironmentVariable(envKey);
        try
        {
            Environment.SetEnvironmentVariable(envKey, "https://env-server.example.com");

            var options = SymbolServerOptions.Create();

            options.ServerUrls.Should().ContainSingle()
                .Which.Should().Be("https://env-server.example.com");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, original);
        }
    }

    [Fact]
    public void Create_WithEnvVarCache_UsesEnvVar()
    {
        var envKey = "DEBUG_MCP_SYMBOL_CACHE";
        var original = Environment.GetEnvironmentVariable(envKey);
        try
        {
            Environment.SetEnvironmentVariable(envKey, "/tmp/test-symbols");

            var options = SymbolServerOptions.Create();

            options.CacheDirectory.Should().Be("/tmp/test-symbols");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, original);
        }
    }

    [Fact]
    public void Create_WithEnvVarNoSymbols_DisablesDownloads()
    {
        var envKey = "DEBUG_MCP_NO_SYMBOLS";
        var original = Environment.GetEnvironmentVariable(envKey);
        try
        {
            Environment.SetEnvironmentVariable(envKey, "1");

            var options = SymbolServerOptions.Create();

            options.Enabled.Should().BeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, original);
        }
    }

    [Fact]
    public void Create_CliOverridesEnvVar()
    {
        var envKey = "DEBUG_MCP_SYMBOL_SERVERS";
        var original = Environment.GetEnvironmentVariable(envKey);
        try
        {
            Environment.SetEnvironmentVariable(envKey, "https://env-server.example.com");

            var options = SymbolServerOptions.Create(symbolServers: "https://cli-server.example.com");

            options.ServerUrls.Should().ContainSingle()
                .Which.Should().Be("https://cli-server.example.com");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, original);
        }
    }

    [Fact]
    public void Create_WithEnvVarTimeout_UsesEnvVar()
    {
        var envKey = "DEBUG_MCP_SYMBOL_TIMEOUT";
        var original = Environment.GetEnvironmentVariable(envKey);
        try
        {
            Environment.SetEnvironmentVariable(envKey, "60");

            var options = SymbolServerOptions.Create();

            options.TimeoutSeconds.Should().Be(60);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, original);
        }
    }

    [Fact]
    public void Create_WithEnvVarMaxSize_UsesEnvVar()
    {
        var envKey = "DEBUG_MCP_SYMBOL_MAX_SIZE";
        var original = Environment.GetEnvironmentVariable(envKey);
        try
        {
            Environment.SetEnvironmentVariable(envKey, "200");

            var options = SymbolServerOptions.Create();

            options.MaxFileSizeMB.Should().Be(200);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, original);
        }
    }

    [Fact]
    public void Create_CliTimeoutOverridesEnvVar()
    {
        var envKey = "DEBUG_MCP_SYMBOL_TIMEOUT";
        var original = Environment.GetEnvironmentVariable(envKey);
        try
        {
            Environment.SetEnvironmentVariable(envKey, "60");

            var options = SymbolServerOptions.Create(symbolTimeout: 90);

            options.TimeoutSeconds.Should().Be(90);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, original);
        }
    }

    [Fact]
    public void Create_CliMaxSizeOverridesEnvVar()
    {
        var envKey = "DEBUG_MCP_SYMBOL_MAX_SIZE";
        var original = Environment.GetEnvironmentVariable(envKey);
        try
        {
            Environment.SetEnvironmentVariable(envKey, "200");

            var options = SymbolServerOptions.Create(symbolMaxSize: 500);

            options.MaxFileSizeMB.Should().Be(500);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, original);
        }
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("")]
    public void Create_WithInvalidEnvVarTimeout_UsesDefault(string value)
    {
        var envKey = "DEBUG_MCP_SYMBOL_TIMEOUT";
        var original = Environment.GetEnvironmentVariable(envKey);
        try
        {
            Environment.SetEnvironmentVariable(envKey, value);

            var options = SymbolServerOptions.Create();

            options.TimeoutSeconds.Should().Be(30, "invalid env var should fall back to default");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, original);
        }
    }

    [Fact]
    public void Create_EmptyServerString_FiltersOutBlanks()
    {
        var options = SymbolServerOptions.Create(symbolServers: "https://valid.com;;  ;https://also-valid.com");

        options.ServerUrls.Should().HaveCount(2);
        options.ServerUrls[0].Should().Be("https://valid.com");
        options.ServerUrls[1].Should().Be("https://also-valid.com");
    }
}
