using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using RealityCheckDesktop.Models;

namespace RealityCheckDesktop.Services;

/// <summary>Starts a short-lived Python worker so YouTube extraction and Ollama inference stay outside the UI process.</summary>
public sealed class YouTubeAnalysisBackend : IAnalysisBackend
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };

    public async Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, IProgress<string> progress, CancellationToken cancellationToken)
    {
        if (!YouTubeUrl.TryNormalize(request.SourceUrl, out var url)) throw new InvalidOperationException("Enter a valid YouTube video link to begin.");
        var workerPath = Path.Combine(AppContext.BaseDirectory, "analysis_worker.py");
        if (!File.Exists(workerPath)) throw new InvalidOperationException("The analysis worker is missing. Rebuild the desktop project.");
        var tempPath = Path.Combine(Path.GetTempPath(), $"reality-check-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempPath);
        using var process = new Process { StartInfo = new ProcessStartInfo("python")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8, WorkingDirectory = AppContext.BaseDirectory
        }};
        process.StartInfo.ArgumentList.Add("-u");
        process.StartInfo.ArgumentList.Add(workerPath);
        process.StartInfo.Environment["PYTHONUTF8"] = "1";
        try
        {
            if (!process.Start()) throw new InvalidOperationException("Could not start the local analysis worker.");
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new
            {
                mode = request.Mode == AnalysisMode.RealityCheck ? "realityCheck" : "aiDetector",
                url, tempDirectory = tempPath
            }));
            process.StandardInput.Close();
            AnalysisResult? result = null;
            string? workerError = null;
            while (await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                var kind = root.GetProperty("kind").GetString();
                if (kind == "progress") progress.Report(root.GetProperty("message").GetString() ?? "Analyzing the video…");
                else if (kind == "error") workerError = root.GetProperty("message").GetString();
                else if (kind == "result")
                {
                    var payload = root.GetProperty("result");
                    result = request.Mode == AnalysisMode.RealityCheck
                        ? payload.Deserialize<RealityCheckResult>(JsonOptions)
                        : payload.Deserialize<AiDetectorResult>(JsonOptions);
                }
            }
            await process.WaitForExitAsync(cancellationToken);
            var stderr = await stderrTask;
            if (result is not null) return result;
            throw new InvalidOperationException(workerError ?? (string.IsNullOrWhiteSpace(stderr)
                ? "The analysis worker stopped without a result."
                : "The YouTube analysis worker failed. Check that Python, yt-dlp and Ollama are available."));
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            throw;
        }
        finally
        {
            try { if (Directory.Exists(tempPath)) Directory.Delete(tempPath, recursive: true); } catch { }
        }
    }
}
