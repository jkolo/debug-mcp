using System.Text.Json;
using AwesomeAssertions;
using DebugMcp.Models.Results;
using DebugMcp.Services;
using DebugMcp.Services.Snapshots;
using DebugMcp.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DebugMcp.Tests.Contract;

/// <summary>
/// FR-021/FR-017: a client that reads only <c>content[0].text</c>, as every client does today,
/// must see the same field names and meanings after US3's typed-result migration. Verified by
/// serializing each tool's returned record the way the SDK does (camelCase, nulls omitted — see
/// data-model.md §1), then parsing and comparing fields, not by string equality — the contract's
/// own wire example (contracts/tool-result-contract.md) is compact JSON, while today's
/// pre-migration hand-rolled tools used <c>WriteIndented = true</c>; indentation was never part
/// of the contract, field presence and values are. One case per migrated tool, added as each tool
/// moves off its hand-rolled <c>JsonSerializer.Serialize(new {...})</c> (T044–T052); this file
/// starts with the pilot (<c>snapshot_delete</c>) as the worked example the rest follows.
/// </summary>
public class LegacyTextContractTests
{
    private static readonly JsonSerializerOptions WireOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    public async Task SnapshotDelete_Success_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<ISnapshotService>();
        var storeMock = new Mock<ISnapshotStore>();
        serviceMock.Setup(s => s.DeleteSnapshot("snap-1")).Returns(true);
        storeMock.Setup(s => s.Count).Returns(3);
        var tool = new SnapshotDeleteTool(serviceMock.Object, storeMock.Object, Mock.Of<ILogger<SnapshotDeleteTool>>());

        var result = await tool.DeleteSnapshotAsync("snap-1");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        parsed.GetProperty("deleted").GetString().Should().Be("snap-1");
        parsed.GetProperty("remaining").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task SnapshotDelete_Failure_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<ISnapshotService>();
        var storeMock = new Mock<ISnapshotStore>();
        serviceMock.Setup(s => s.DeleteSnapshot("snap-missing")).Returns(false);
        var tool = new SnapshotDeleteTool(serviceMock.Object, storeMock.Object, Mock.Of<ILogger<SnapshotDeleteTool>>());

        var result = await tool.DeleteSnapshotAsync("snap-missing");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("SNAPSHOT_NOT_FOUND");
        parsed.GetProperty("error").GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// <c>ProcessIoManager</c> is a sealed concrete class with no interface, so it cannot be
    /// mocked with Moq the way <c>ISnapshotService</c>/<c>ISnapshotStore</c> are above. The
    /// success branch of <c>process_read_output</c>/<c>process_write_input</c> requires
    /// <c>HasProcess == true</c>, i.e. a live redirected OS process — spawning one here would
    /// trade a deterministic contract test for a flaky one (exactly what this project's
    /// Integration/Performance tiers already isolate away from Unit/Contract). The success cases
    /// below instead construct the result record directly with the same values the tool's own
    /// switch branches would produce, and verify only the wire-shape risk this migration
    /// introduces — that the C# record serializes to the exact legacy field names/omissions.
    /// The failure cases go through the real tool with a real (but unattached) IO manager, which
    /// needs no live process and is fully deterministic.
    /// </summary>
    [Fact]
    public void ProcessReadOutput_Success_PreservesLegacyFieldNames()
    {
        // Mirrors the "both" branch ProcessReadOutputTool.ReadOutputAsync builds today.
        var result = new ProcessReadOutputResult(
            Success: true,
            Stdout: "out",
            Stderr: "err",
            StdoutBytes: 3,
            StderrBytes: 3);
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        parsed.GetProperty("stdout").GetString().Should().Be("out");
        parsed.GetProperty("stderr").GetString().Should().Be("err");
        parsed.GetProperty("stdoutBytes").GetInt32().Should().Be(3);
        parsed.GetProperty("stderrBytes").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task ProcessReadOutput_Failure_PreservesLegacyFieldNames()
    {
        var ioManager = new ProcessIoManager(NullLogger<ProcessIoManager>.Instance);
        var tool = new ProcessReadOutputTool(ioManager, NullLogger<ProcessReadOutputTool>.Instance);

        var result = await tool.ReadOutputAsync();
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("NO_SESSION");
        parsed.GetProperty("error").GetProperty("message").GetString().Should().NotBeNullOrEmpty();
        parsed.TryGetProperty("stdout", out _).Should().BeFalse();
    }

    [Fact]
    public void ProcessWriteInput_Success_PreservesLegacyFieldNames()
    {
        // Mirrors the success branch ProcessWriteInputTool.WriteInputAsync builds today.
        var result = new ProcessWriteInputResult(
            Success: true,
            BytesWritten: 5,
            StdinClosed: true);
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        parsed.GetProperty("bytesWritten").GetInt32().Should().Be(5);
        parsed.GetProperty("stdinClosed").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ProcessWriteInput_Failure_PreservesLegacyFieldNames()
    {
        var ioManager = new ProcessIoManager(NullLogger<ProcessIoManager>.Instance);
        var tool = new ProcessWriteInputTool(ioManager, NullLogger<ProcessWriteInputTool>.Instance);

        var result = await tool.WriteInputAsync("hello");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("NO_SESSION");
        parsed.GetProperty("error").GetProperty("message").GetString().Should().NotBeNullOrEmpty();
        parsed.TryGetProperty("bytesWritten", out _).Should().BeFalse();
    }
}
