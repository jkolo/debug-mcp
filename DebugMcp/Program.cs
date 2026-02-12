using System.CommandLine;
using System.Reflection;
using DebugMcp.Infrastructure;
using DebugMcp.Services;
using DebugMcp.Services.Breakpoints;
using DebugMcp.Services.CodeAnalysis;
using DebugMcp.Services.Completions;
using DebugMcp.Services.Inspection;
using DebugMcp.Services.Resources;
using DebugMcp.Services.Snapshots;
using DebugMcp.Services.Symbols;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
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

var noSymbolsOption = new Option<bool>("--no-symbols")
{
    Description = "Disable automatic PDB symbol downloads from symbol servers"
};
rootCommand.Options.Add(noSymbolsOption);

var symbolServersOption = new Option<string?>("--symbol-servers")
{
    Description = "Semicolon-separated list of SSQP symbol server URLs (default: Microsoft + NuGet)"
};
rootCommand.Options.Add(symbolServersOption);

var symbolCacheOption = new Option<string?>("--symbol-cache")
{
    Description = "Directory path for persistent PDB symbol cache (default: ~/.debug-mcp/symbols)"
};
rootCommand.Options.Add(symbolCacheOption);

var symbolTimeoutOption = new Option<int?>("--symbol-timeout")
{
    Description = "Per-file download timeout in seconds (default: 30)"
};
rootCommand.Options.Add(symbolTimeoutOption);

var symbolMaxSizeOption = new Option<int?>("--symbol-max-size")
{
    Description = "Maximum PDB file size to download in MB (default: 100)"
};
rootCommand.Options.Add(symbolMaxSizeOption);

rootCommand.SetAction(async parseResult =>
{
    var enableStderr = parseResult.GetValue(stderrOption);
    var disableRoslyn = parseResult.GetValue(noRoslynOption);
    var noSymbols = parseResult.GetValue(noSymbolsOption);
    var symbolServers = parseResult.GetValue(symbolServersOption);
    var symbolCache = parseResult.GetValue(symbolCacheOption);
    var symbolTimeout = parseResult.GetValue(symbolTimeoutOption);
    var symbolMaxSize = parseResult.GetValue(symbolMaxSizeOption);

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

    // Register exception autopsy service (022-exception-autopsy)
    builder.Services.AddSingleton<IExceptionAutopsyService, ExceptionAutopsyService>();

    // Register async stack trace service (026-async-stack-traces)
    builder.Services.AddSingleton<IAsyncStackTraceService, AsyncStackTraceService>();

    // Register snapshot services (027-state-snapshot-diff)
    builder.Services.AddSingleton<ISnapshotStore, SnapshotStore>();
    builder.Services.AddSingleton<ISnapshotService, SnapshotService>();

    // Register collection & object summarizer services (028-collection-object-summarizer)
    builder.Services.AddSingleton<ICollectionAnalyzer, CollectionAnalyzer>();
    builder.Services.AddSingleton<IObjectSummarizer, ObjectSummarizer>();

    // Register resource services (019-mcp-resources)
    builder.Services.AddSingleton<ThreadSnapshotCache>();
    builder.Services.AddSingleton<AllowedSourcePaths>();
    builder.Services.AddSingleton<ResourceNotifier, McpResourceNotifier>();

    // Register symbol server services (021-symbol-server)
    var symbolOptions = SymbolServerOptions.Create(symbolServers, symbolCache, noSymbols, symbolTimeout, symbolMaxSize);
    builder.Services.AddSingleton(symbolOptions);
    builder.Services.AddSingleton<PeDebugInfoReader>();
    builder.Services.AddSingleton(sp => new PersistentSymbolCache(
        symbolOptions.CacheDirectory,
        sp.GetRequiredService<ILogger<PersistentSymbolCache>>()));
    builder.Services.AddSingleton(sp => new SymbolServerClient(
        new HttpClient(),
        symbolOptions,
        sp.GetRequiredService<ILogger<SymbolServerClient>>()));
    builder.Services.AddSingleton<ISymbolResolver, SymbolResolver>();

    // Register completion services (020-mcp-completions)
    builder.Services.AddSingleton<ExpressionCompletionProvider>();

    // Register code analysis services (015-roslyn-code-analysis)
    if (!disableRoslyn)
    {
        builder.Services.AddSingleton<ICodeAnalysisService, CodeAnalysisService>();
    }

    // Configure MCP server with stdio transport, logging, and resources capabilities
    var toolTypes = typeof(Program).Assembly.GetTypes()
        .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
        .Where(t => !disableRoslyn || !t.Name.StartsWith("Code", StringComparison.Ordinal));

    builder.Services
        .AddMcpServer(options =>
        {
            options.Capabilities ??= new();
            options.Capabilities.Logging = new();
            options.Capabilities.Tools = new()
            {
                ListChanged = true
            };
            options.Capabilities.Resources = new()
            {
                Subscribe = true,
                ListChanged = true
            };
            options.Capabilities.Completions = new();
        })
        .WithStdioServerTransport()
        .WithResources<DebuggerResourceProvider>()
        .WithSubscribeToResourcesHandler((request, ct) =>
        {
            if (request.Params?.Uri is { } uri)
            {
                var notifier = request.Services!.GetRequiredService<ResourceNotifier>();
                notifier.Subscribe(uri);
            }
            return new ValueTask<EmptyResult>(new EmptyResult());
        })
        .WithUnsubscribeFromResourcesHandler((request, ct) =>
        {
            if (request.Params?.Uri is { } uri)
            {
                var notifier = request.Services!.GetRequiredService<ResourceNotifier>();
                notifier.Unsubscribe(uri);
            }
            return new ValueTask<EmptyResult>(new EmptyResult());
        })
        .WithTools(toolTypes)
        .WithCompleteHandler(async (request, ct) =>
        {
            var provider = request.Services!.GetRequiredService<ExpressionCompletionProvider>();
            return await provider.GetCompletionsAsync(request.Params!, ct);
        });

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

    if (!symbolOptions.Enabled)
    {
        logger.LogInformation("Symbol server downloads disabled");
    }
    else
    {
        logger.LogInformation("Symbol servers: {Servers}", string.Join(", ", symbolOptions.ServerUrls));
        logger.LogInformation("Symbol cache: {CacheDir}", symbolOptions.CacheDirectory);
    }

    await host.RunAsync();
});

var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();
