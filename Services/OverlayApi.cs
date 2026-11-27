using RealityCheckDesktop.Models;

namespace RealityCheckDesktop.Services;

/// <summary>Public result-ingestion API for an external analysis worker.</summary>
public sealed class OverlayApi
{
    private readonly OverlayState _state;
    private Guid? _activeRequest;
    public OverlayApi(OverlayState? state = null) => _state = state ?? new OverlayState();
    public OverlayState State => _state;
    public AnalysisRequest Begin(AnalysisMode mode, string sourceUrl = "")
    {
        var request = new AnalysisRequest(Guid.NewGuid(), mode, DateTimeOffset.UtcNow, sourceUrl);
        _activeRequest = request.RequestId; _state.Begin(mode); return request;
    }
    public void ReportProgress(Guid requestId, string message) { EnsureActive(requestId); _state.ReportProgress(message); }
    public void SubmitResult(AnalysisEnvelope envelope)
    {
        EnsureActive(envelope.RequestId);
        if (envelope.Mode != _state.Current.Mode) throw new ArgumentException("Envelope mode does not match the active request.");
        _state.Complete(envelope.Result); _activeRequest = null;
    }
    public void ReportFailure(AnalysisFailure failure)
    {
        EnsureActive(failure.RequestId); _state.Fail(failure.UserMessage); _activeRequest = null;
    }
    public void Select(AnalysisMode mode) { _activeRequest = null; _state.Select(mode); }
    public void Reset() { _activeRequest = null; _state.Reset(); }
    private void EnsureActive(Guid id)
    {
        if (_activeRequest != id) throw new InvalidOperationException("The request is no longer active.");
    }
}
