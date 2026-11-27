namespace RealityCheckDesktop.Models;

public sealed record IncidentRecord(
    string Id,
    string Mode,
    string Title,
    string SourceUrl,
    string Verdict,
    int? Score,
    string Summary,
    string Explanation,
    string DetailsJson,
    DateTimeOffset CreatedAt)
{
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? "Untitled incident" : Title;

    public string DisplaySubtitle =>
        $"{Mode}  ·  {CreatedAt.ToLocalTime():g}  ·  {Verdict}" +
        (Score is int score ? $"  ·  {score}%" : "");
}

public sealed record IncidentMessage(long Id, string IncidentId, string Role, string Content, DateTimeOffset CreatedAt);
