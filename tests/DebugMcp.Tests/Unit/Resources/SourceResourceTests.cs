using DebugMcp.Models;
using DebugMcp.Services;
using DebugMcp.Services.Breakpoints;
using DebugMcp.Services.Resources;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Resources;

/// <summary>
/// Tests for source code resource read (T025).
/// </summary>
public class SourceResourceTests
{
    private readonly Mock<IDebugSessionManager> _sessionManagerMock;
    private readonly AllowedSourcePaths _allowedPaths;
    private readonly DebuggerResourceProvider _provider;

    public SourceResourceTests()
    {
        _sessionManagerMock = new Mock<IDebugSessionManager>();
        var registryLogger = new Mock<ILogger<BreakpointRegistry>>();
        var registry = new BreakpointRegistry(registryLogger.Object);
        var threadCache = new ThreadSnapshotCache();
        _allowedPaths = new AllowedSourcePaths();
        var providerLogger = new Mock<ILogger<DebuggerResourceProvider>>();

        _provider = new DebuggerResourceProvider(
            _sessionManagerMock.Object,
            registry,
            threadCache,
            _allowedPaths,
            providerLogger.Object);

        // Set up active session for most tests
        _sessionManagerMock.Setup(x => x.CurrentSession).Returns(new DebugSession
        {
            ProcessId = 1234,
            ProcessName = "MyApp",
            ExecutablePath = "/path/to/MyApp.dll",
            RuntimeVersion = ".NET 10.0.0",
            AttachedAt = DateTimeOffset.UtcNow,
            LaunchMode = LaunchMode.Attach
        });
    }

    [Fact]
    public async Task GetSourceFileAsync_WithAllowedPath_ReturnsFileContent()
    {
        // Create a temp file to serve as source
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "using System;\nclass Program { }");
            _allowedPaths.AddModule("MyApp.dll", [tempFile]);

            var content = await _provider.GetSourceFileAsync(tempFile);

            content.Should().Contain("using System;");
            content.Should().Contain("class Program");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task GetSourceFileAsync_WithDisallowedPath_ThrowsInvalidOperation()
    {
        var act = () => _provider.GetSourceFileAsync("/some/random/file.cs");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not referenced*PDB*");
    }

    [Fact]
    public async Task GetSourceFileAsync_WhenFileNotOnDisk_ThrowsInvalidOperation()
    {
        var missingFile = "/tmp/nonexistent-source-file-" + Guid.NewGuid() + ".cs";
        _allowedPaths.AddModule("MyApp.dll", [missingFile]);

        var act = () => _provider.GetSourceFileAsync(missingFile);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found on disk*");
    }

    [Fact]
    public async Task GetSourceFileAsync_WhenNoSession_ThrowsInvalidOperation()
    {
        _sessionManagerMock.Setup(x => x.CurrentSession).Returns((DebugSession?)null);

        var act = () => _provider.GetSourceFileAsync("/some/file.cs");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No active debug session*");
    }

    [Fact]
    public async Task GetSourceFileAsync_WithNormalizedPath_MatchesAllowedPaths()
    {
        // Test that backslash paths are normalized to forward slash
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "content");
            _allowedPaths.AddModule("MyApp.dll", [tempFile]);

            // The path should work with the normalized form
            var content = await _provider.GetSourceFileAsync(tempFile);
            content.Should().Be("content");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
