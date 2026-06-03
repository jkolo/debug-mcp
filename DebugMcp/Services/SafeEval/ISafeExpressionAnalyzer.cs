namespace DebugMcp.Services.SafeEval;

public interface ISafeExpressionAnalyzer
{
    SafeAnalysisResult Analyze(string expression);
}
