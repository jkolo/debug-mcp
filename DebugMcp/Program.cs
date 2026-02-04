using System.CommandLine;
using DebugMcp.Infrastructure;
using DebugMcp.Services;
using DebugMcp.Services.Breakpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var rootCommand = new RootCommand("MCP server for debugging .NET applications");

var stderrOption = new Option<bool>("--stderr-logging", "-s")
{
    Description = "Also write logs to stderr alongside MCP notifications"
};
rootCommand.Options.Add(stderrOption);

rootCommand.SetAction(async parseResult =>
{
    var enableStderr = parseResult.GetValue(stderrOption);

    var builder = Host.CreateApplicationBuilder([]);

    // Configure logging options
    var loggingOptions = new LoggingOptions
    {
        EnableStderr = enableStderr,
        DefaultMinLevel = McpLogLevel.Info,
        MaxPayloadBytes = 64 * 1024
    };
    builder.Services.AddSingleton(loggingOptions);

    // Configure logging - use MCP logger, optionally with stderr
    builder.Logging.ClearProviders();
    if (enableStderr)
    {
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });
    }
    builder.Logging.SetMinimumLevel(LogLevel.Information);

    // Register debug services
    builder.Services.AddSingleton<IProcessDebugger, ProcessDebugger>();
    builder.Services.AddSingleton<IDebugSessionManager, DebugSessionManager>();

    // Register breakpoint services
    builder.Services.AddSingleton<PdbSymbolCache>();
    builder.Services.AddSingleton<IPdbSymbolReader, PdbSymbolReader>();
    builder.Services.AddSingleton<BreakpointRegistry>();
    builder.Services.AddSingleton<SimpleConditionEvaluator>();
    builder.Services.AddSingleton<IConditionEvaluator>(sp =>
        new DebuggerConditionEvaluator(
            sp.GetRequiredService<SimpleConditionEvaluator>(),
            sp.GetRequiredService<ILogger<DebuggerConditionEvaluator>>()));
    builder.Services.AddSingleton<IBreakpointManager, BreakpointManager>();

    // Configure MCP server with stdio transport and logging capability
    builder.Services
        .AddMcpServer(options =>
        {
            options.Capabilities ??= new();
            options.Capabilities.Logging = new();
        })
        .WithStdioServerTransport()
        .WithToolsFromAssembly();

    // Add MCP logger provider (must be after AddMcpServer)
    builder.Services.AddSingleton<ILoggerProvider, McpLoggerProvider>();

    var host = builder.Build();
    await host.RunAsync();
});

var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();
