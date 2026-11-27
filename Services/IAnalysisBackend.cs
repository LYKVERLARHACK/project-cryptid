using RealityCheckDesktop.Models;

namespace RealityCheckDesktop.Services;

/// <summary>Implement with an out-of-process backend (for example named pipes or localhost IPC). No screen content is persisted by this UI.</summary>
public interface IAnalysisBackend
{
    Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, IProgress<string> progress, CancellationToken cancellationToken);
}
