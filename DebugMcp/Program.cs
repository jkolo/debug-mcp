using System.CommandLine;
using System.Reflection;
using DebugMcp.Infrastructure;
using DebugMcp.Services;
using DebugMcp.Services.Breakpoints;
using DebugMcp.Services.CodeAnalysis;
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

var noRoslynOption = new Option<bool>("--no-roslyn", "-r")
{
    Description = "Disable Roslyn code analysis tools (use when JetBrains MCP provides equivalent functionality)"
};
rootCommand.Options.Add(noRoslynOption);

rootCommand.SetAction(async parseResult =>
{
    var enableStderr = parseResult.GetValue(stderrOption);
    var disableRoslyn = parseResult.GetValue(noRoslynOption);

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
    builder.Services.AddSingleton<ProcessIoManager>();
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
    builder.Services.AddSingleton<IBreakpointNotifier, BreakpointNotifier>();
    builder.Services.AddSingleton<LogMessageEvaluator>();
    builder.Services.AddSingleton<IBreakpointManager, BreakpointManager>();

    // Register code analysis services (015-roslyn-code-analysis)
    if (!disableRoslyn)
    {
        builder.Services.AddSingleton<ICodeAnalysisService, CodeAnalysisService>();
    }

    // Collect MCP tool types, optionally excluding Roslyn code analysis tools
    var codeAnalysisToolNames = new HashSet<string>
    {
        nameof(DebugMcp.Tools.CodeLoadTool),
        nameof(DebugMcp.Tools.CodeGoToDefinitionTool),
        nameof(DebugMcp.Tools.CodeFindUsagesTool),
        nameof(DebugMcp.Tools.CodeFindAssignmentsTool),
        nameof(DebugMcp.Tools.CodeGetDiagnosticsTool),
    };

    var toolTypes = typeof(Program).Assembly.GetTypes()
        .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
        .Where(t => !disableRoslyn || !codeAnalysisToolNames.Contains(t.Name))
        .ToList();

    // Configure MCP server with stdio transport and logging capability
    builder.Services
        .AddMcpServer(options =>
        {
            options.Capabilities ??= new();
            options.Capabilities.Logging = new();
        })
        .WithStdioServerTransport()
        .WithTools(toolTypes);

    // Add MCP logger provider (must be after AddMcpServer)
    builder.Services.AddSingleton<ILoggerProvider, McpLoggerProvider>();

    var host = builder.Build();

    var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DebugMcp");
    if (disableRoslyn)
    {
        logger.LogInformation("Roslyn code analysis tools disabled (--no-roslyn)");
    }
    else
    {
        logger.LogInformation("Roslyn code analysis tools enabled");
    }

    await host.RunAsync();
});

var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();
