namespace DebugMcp.Services.SafeEval;

public enum RejectionCategory
{
    MethodCall,
    ObjectCreation,
    Assignment,
    ParseError
}

public sealed record SafeEvalRejection(
    RejectionCategory Category,
    string OffendingExpression,
    string Message);

public sealed record SafeAnalysisResult(bool IsAllowed, SafeEvalRejection? Rejection)
{
    public static SafeAnalysisResult Allowed() =>
        new(IsAllowed: true, Rejection: null);

    public static SafeAnalysisResult Rejected(SafeEvalRejection rejection) =>
        new(IsAllowed: false, Rejection: rejection);
}
