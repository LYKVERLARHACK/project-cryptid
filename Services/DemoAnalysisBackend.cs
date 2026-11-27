using RealityCheckDesktop.Models;

namespace RealityCheckDesktop.Services;

/// <summary>Illustrative fixtures only. Activated exclusively with the explicit --demo switch.</summary>
public sealed class DemoAnalysisBackend : IAnalysisBackend
{
    public async Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, IProgress<string> progress, CancellationToken cancellationToken)
    {
        progress.Report("Reviewing the visible claims…"); await Task.Delay(900, cancellationToken);
        progress.Report(request.Mode == AnalysisMode.RealityCheck ? "Comparing with sample evidence…" : "Checking visual consistency…");
        await Task.Delay(950, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (request.Mode == AnalysisMode.RealityCheck)
            return new RealityCheckResult(35, "Viable methods, unrealistic benchmarks",
                "The approach is technically possible, but the promised results are substantially overstated. This is sample content for the interface; no claim or source has been checked.",
                [new("The method is technically possible", "Plausible", 68), new("The stated results are typical", "Not verified", 12)], [], now);
        return new AiDetectorResult(78, AuthenticityVerdict.Suspicious, "Some visual signs need a closer look",
            "Sample findings suggest possible visual manipulation. This illustrative result is not an actual detection, and audio was not analyzed.",
            [new("Frame consistency", FindingState.Inconclusive, "Sample finding only"), new("Facial details", FindingState.Inconclusive), new("Audio", FindingState.Unavailable, "Audio analysis is not included")], [], now);
    }
}
