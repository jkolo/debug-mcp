using System.IO.Pipelines;
using System.Text.Json.Nodes;
using DebugMcp.Services.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;

namespace DebugMcp.Tests.Support;

/// <summary>
/// Runs a real MCP server (with the MCP Tasks extension wired exactly as Program.cs wires it)
/// and a real MCP client over an in-process, in-memory duplex transport
/// (<see cref="ModelContextProtocol.Server.StreamServerTransport"/> / <c>StreamClientTransport</c>
/// paired over two <see cref="Pipe"/>s). This makes wire-level MCP Tasks behavior — opt-in gating,
/// task lifecycle, cancellation propagation, expired/unknown task ids — unit-test-observable instead
/// of requiring a stdio smoke test (research.md R6 originally assumed no in-process transport existed;
/// it does: see data-model.md's US2 correction notes).
/// </summary>
public sealed class InProcessMcpHarness : IAsyncDisposable
{
    private readonly IHostedService _hostedService;

    private InProcessMcpHarness(McpClient client, IHostedService hostedService, InMemoryMcpTaskStore rawStore, FakeQualifyingTool tool)
    {
        Client = client;
        _hostedService = hostedService;
        RawStore = rawStore;
        Tool = tool;
    }

    public McpClient Client { get; }

    /// <summary>The undecorated SDK store — inspect directly to assert on state the client-facing wire protocol doesn't expose.</summary>
    public InMemoryMcpTaskStore RawStore { get; }

    public FakeQualifyingTool Tool { get; }

    public static async Task<InProcessMcpHarness> StartAsync(
        bool declareTasksCapability = true,
        TimeSpan? taskTimeToLive = null)
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));

        // Bind the exact instance below via WithTools(builder, target) — the generic
        // WithTools<T>() overload does not guarantee it resolves the same DI singleton per
        // call, which silently broke this harness's per-test Gate/ProgressReports state.
        var tool = new FakeQualifyingTool();

        var rawStore = new InMemoryMcpTaskStore();
        if (taskTimeToLive is { } ttl)
        {
            rawStore.DefaultTimeToLive = ttl;
        }

        var store = new ExpiryAwareTaskStore(rawStore);

        services.AddMcpServer(_ => { })
            .WithTools(tool)
            .WithStreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream())
            .WithTasks(store, opts =>
                opts.ExecutionModeSelector = ctx => ctx.Params?.Name == "slow_qualifying_tool"
                    ? McpTaskExecutionMode.Optional
                    : McpTaskExecutionMode.Synchronous);

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetRequiredService<IHostedService>();
        await hostedService.StartAsync(CancellationToken.None);

        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        var clientTransport = new StreamClientTransport(
            clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream(), loggerFactory);

        var clientOptions = new McpClientOptions
        {
            ClientInfo = new Implementation { Name = "test-harness", Version = "1.0" },
        };
        if (declareTasksCapability)
        {
            clientOptions.Capabilities = new ClientCapabilities
            {
                Extensions = new Dictionary<string, object?> { [TasksProtocol.ExtensionId] = new JsonObject() }!,
            };
        }

        var client = await McpClient.CreateAsync(clientTransport, clientOptions, loggerFactory, CancellationToken.None);
        return new InProcessMcpHarness(client, hostedService, rawStore, tool);
    }

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
        await _hostedService.StopAsync(CancellationToken.None);
    }
}
