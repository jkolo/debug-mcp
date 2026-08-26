namespace DebugTestApp.FaultScenarios;

/// <summary>
/// A call chain with no null/message/collection evidence anywhere — only the innermost-user-frame
/// heuristic distinguishes a winner, exercising it in isolation plus deterministic tie-breaking
/// (FrameIndex ascending) for any frames that remain equally (un)scored. Fault frame:
/// <see cref="Step3"/> (the actual throw site, innermost frame).
/// </summary>
public static class DeepChainManyExternalFrames
{
    public static void Run() => Step1();

    private static void Step1() => Step2();

    private static void Step2() => Step3();

    private static void Step3()
    {
        throw new InvalidOperationException("Unexpected state reached after several steps.");
    }
}
