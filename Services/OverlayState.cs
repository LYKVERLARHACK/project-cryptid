using RealityCheckDesktop.Models;

namespace RealityCheckDesktop.Services;

public sealed record OverlaySnapshot(AnalysisMode Mode, AnalysisStatus Status, string? Progress = null,
    AnalysisResult? Result = null, string? Error = null);

/// <summary>Small deterministic state model shared by UI and positioning/state tests.</summary>
public sealed class OverlayState
{
    public OverlaySnapshot Current { get; private set; } = new(AnalysisMode.RealityCheck, AnalysisStatus.Idle);
    public event EventHandler<OverlaySnapshot>? Changed;
    public void Select(AnalysisMode mode) => Set(new(mode, AnalysisStatus.Idle));
    public void Begin(AnalysisMode mode) => Set(new(mode, AnalysisStatus.Checking, "Preparing a private analysis…"));
    public void ReportProgress(string message)
    {
        if (Current.Status != AnalysisStatus.Checking) throw new InvalidOperationException("Progress is only valid while checking.");
        Set(Current with { Progress = message, Error = null });
    }
    public void Complete(AnalysisResult result)
    {
        if (Current.Status != AnalysisStatus.Checking) throw new InvalidOperationException("A result can only complete an active analysis.");
        if ((Current.Mode == AnalysisMode.RealityCheck) != (result is RealityCheckResult)) throw new ArgumentException("Result type does not match selected mode.");
        Set(Current with { Status = AnalysisStatus.Complete, Progress = null, Result = result, Error = null });
    }
    public void Fail(string message)
    {
        if (Current.Status != AnalysisStatus.Checking) throw new InvalidOperationException("An error can only end an active analysis.");
        Set(Current with { Status = AnalysisStatus.Error, Progress = null, Result = null, Error = message });
    }
    public void Reset() => Set(new(Current.Mode, AnalysisStatus.Idle));
    private void Set(OverlaySnapshot snapshot) { Current = snapshot; Changed?.Invoke(this, snapshot); }
}
