namespace RealityCheckDesktop.Models;

public enum AnalysisMode { RealityCheck, AiDetector }
public enum AnalysisStatus { Idle, Checking, Complete, Error }
public enum AuthenticityVerdict { LikelyAuthentic, Suspicious, LikelyGenerated, Manipulated, Inconclusive }
public enum FindingState { Detected, NotDetected, Unavailable, Inconclusive }

public sealed record EvidenceSource(string Title, Uri Url, string? Publisher = null, string? Detail = null);
public sealed record ClaimAssessment(string Claim, string Assessment, int ConfidencePercent);
public sealed record DetectorFinding(string Label, FindingState State, string? Detail = null, string? Timestamp = null);
public abstract record AnalysisResult(string Summary, DateTimeOffset CompletedAt);
public sealed record RealityCheckResult(int RealismPercent, string VerdictTitle, string Explanation,
    IReadOnlyList<ClaimAssessment> Claims, IReadOnlyList<EvidenceSource> Sources, DateTimeOffset CompletedAt, bool WebVerified = false,
    bool EvidenceSearched = false,
    string VideoTitle = "", string VideoUrl = "")
    : AnalysisResult(Explanation, CompletedAt);
public sealed record AiDetectorResult(int AiLikelihoodPercent, AuthenticityVerdict Verdict, string VerdictTitle,
    string Explanation, IReadOnlyList<DetectorFinding> Findings, IReadOnlyList<EvidenceSource> Sources,
    DateTimeOffset CompletedAt, bool AudioAnalyzed = false, bool ForensicModelUsed = false,
    string VideoTitle = "", string VideoUrl = "", string VideoSummary = "", string TranscriptSource = "")
    : AnalysisResult(Explanation, CompletedAt);
public sealed record AnalysisRequest(Guid RequestId, AnalysisMode Mode, DateTimeOffset RequestedAt, string SourceUrl = "");
public sealed record AnalysisEnvelope(Guid RequestId, AnalysisMode Mode, AnalysisResult Result);
public sealed record AnalysisFailure(Guid RequestId, string UserMessage, string? DiagnosticCode = null);
