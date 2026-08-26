using System.Text.RegularExpressions;

namespace DebugMcp.Tests.Contract;

/// <summary>
/// FR-022, SC-013: the server MUST NOT call any language model, and MUST NOT require or accept a
/// model provider credential. All enrichment (069's User Story 4, <c>SuspicionRanker</c>) is
/// computed from data the server already holds. FR-022 is this feature's defining architectural
/// decision and the one requirement with no natural implementation task — without this guard
/// nothing would ever detect its violation (e.g. a future contributor adding an LLM-backed
/// heuristic to "improve" ranking accuracy).
/// </summary>
public sealed class NoModelDependencyTests
{
    /// <summary>Package name fragments identifying a language-model provider SDK.</summary>
    private static readonly string[] ModelProviderPackagePatterns =
    [
        "OpenAI", "Anthropic", "Azure.AI.OpenAI", "Azure.AI.Inference", "Google.Cloud.AI",
        "Google.GenerativeAI", "Cohere", "HuggingFace", "LangChain", "SemanticKernel",
        "Microsoft.SemanticKernel", "Mistral", "Ollama", "AWS.Bedrock", "Amazon.Bedrock",
    ];

    /// <summary>CLI option / config-key fragments suggesting a model provider credential.</summary>
    private static readonly string[] ModelCredentialPatterns =
    [
        "api-key", "api_key", "apikey", "model-key", "model-provider", "model-credential",
        "openai-key", "anthropic-key", "llm-key", "llm-token", "llm-endpoint",
    ];

    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void DirectoryPackagesProps_ReferencesNoModelProviderPackage()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot, "Directory.Packages.props"));

        foreach (var pattern in ModelProviderPackagePatterns)
        {
            content.Should().NotContainEquivalentOf(pattern,
                because: $"the server must not depend on a model-provider SDK ('{pattern}') — FR-022");
        }
    }

    [Fact]
    public void DebugMcpAssembly_ReferencesNoModelProviderAssembly()
    {
        var assembly = typeof(DebugMcp.Tools.DebugLaunchTool).Assembly;
        var referencedNames = assembly.GetReferencedAssemblies().Select(a => a.Name ?? "").ToList();

        foreach (var pattern in ModelProviderPackagePatterns)
        {
            referencedNames.Should().NotContain(
                name => name.Contains(pattern, StringComparison.OrdinalIgnoreCase),
                because: $"the built DebugMcp.dll must not reference a model-provider assembly ('{pattern}') — FR-022");
        }
    }

    [Fact]
    public void ProgramCs_ExposesNoModelCredentialConfigurationPath()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot, "DebugMcp", "Program.cs"));

        foreach (var pattern in ModelCredentialPatterns)
        {
            Regex.IsMatch(content, Regex.Escape(pattern), RegexOptions.IgnoreCase).Should().BeFalse(
                because: $"the server must not accept a model-provider credential via any CLI option or environment variable ('{pattern}') — FR-022, SC-013");
        }
    }

    [Fact]
    public void SuspicionRanker_HasNoHttpOrModelClientDependency()
    {
        // The one place in this feature that could plausibly grow a model dependency later:
        // the deterministic ranker itself. Its constructor must stay parameterless — anything
        // it needed to inject (an HttpClient, a model client) would show up here first.
        var constructors = typeof(DebugMcp.Services.Inspection.SuspicionRanker)
            .GetConstructors();

        constructors.Should().ContainSingle(because: "SuspicionRanker must remain a plain, dependency-free deterministic function of its inputs")
            .Which.GetParameters().Should().BeEmpty();
    }
}
