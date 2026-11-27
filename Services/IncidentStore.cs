using System.IO;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using RealityCheckDesktop.Models;

namespace RealityCheckDesktop.Services;

public sealed class IncidentStore
{
    private const string Schema = """
        PRAGMA foreign_keys = ON;
        CREATE TABLE IF NOT EXISTS incidents (
            id TEXT PRIMARY KEY NOT NULL,
            mode TEXT NOT NULL,
            title TEXT NOT NULL,
            source_url TEXT NOT NULL,
            verdict TEXT NOT NULL,
            score INTEGER NULL,
            summary TEXT NOT NULL,
            explanation TEXT NOT NULL,
            details_json TEXT NOT NULL,
            created_at TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_incidents_created_at ON incidents(created_at DESC);
        CREATE TABLE IF NOT EXISTS incident_messages (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            incident_id TEXT NOT NULL REFERENCES incidents(id) ON DELETE CASCADE,
            role TEXT NOT NULL CHECK (role IN ('user', 'assistant')),
            content TEXT NOT NULL,
            created_at TEXT NOT NULL
        );
        """;

    private readonly string _databasePath;
    private readonly string _connectionString;

    public IncidentStore(string? databasePath = null)
    {
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localData) && string.IsNullOrWhiteSpace(databasePath))
        {
            throw new InvalidOperationException("The local application data folder is unavailable.");
        }

        _databasePath = databasePath ?? Path.Combine(localData, "RealityCheckDesktop", "incidents.sqlite3");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = 5
        }.ToString();
    }

    public string DatabasePath => _databasePath;

    public void Save(AnalysisRequest request, AnalysisResult result)
    {
        var incident = CreateIncident(request, result);
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO incidents
                (id, mode, title, source_url, verdict, score, summary, explanation, details_json, created_at)
            VALUES
                (@id, @mode, @title, @source_url, @verdict, @score, @summary, @explanation, @details_json, @created_at);
            """;
        AddIncidentParameters(command, incident);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public void SaveFailure(AnalysisRequest request, string message)
    {
        var incident = new IncidentRecord(
            request.RequestId.ToString("N"),
            request.Mode.ToString(),
            "Analysis failed",
            request.SourceUrl,
            "Analysis failed",
            null,
            message,
            message,
            JsonSerializer.Serialize(new { error = message }),
            DateTimeOffset.UtcNow);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO incidents
                (id, mode, title, source_url, verdict, score, summary, explanation, details_json, created_at)
            VALUES
                (@id, @mode, @title, @source_url, @verdict, @score, @summary, @explanation, @details_json, @created_at);
            """;
        AddIncidentParameters(command, incident);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public IReadOnlyList<IncidentRecord> GetRecentIncidents(int limit = 100)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, mode, title, source_url, verdict, score, summary, explanation, details_json, created_at
            FROM incidents
            ORDER BY created_at DESC
            LIMIT @limit;
            """;
        command.Parameters.AddWithValue("@limit", Math.Clamp(limit, 1, 1000));
        using var reader = command.ExecuteReader();
        var incidents = new List<IncidentRecord>();
        while (reader.Read())
        {
            incidents.Add(ReadIncident(reader));
        }
        return incidents;
    }

    public IReadOnlyList<IncidentMessage> GetMessages(string incidentId, int limit = 40)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, incident_id, role, content, created_at
            FROM (
                SELECT id, incident_id, role, content, created_at
                FROM incident_messages
                WHERE incident_id = @incident_id
                ORDER BY id DESC
                LIMIT @limit
            )
            ORDER BY id;
            """;
        command.Parameters.AddWithValue("@incident_id", incidentId);
        command.Parameters.AddWithValue("@limit", Math.Clamp(limit, 1, 200));
        using var reader = command.ExecuteReader();
        var messages = new List<IncidentMessage>();
        while (reader.Read())
        {
            messages.Add(new IncidentMessage(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture)));
        }
        return messages;
    }

    public void AddExchange(string incidentId, string question, string answer)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        InsertMessage(connection, transaction, incidentId, "user", question);
        InsertMessage(connection, transaction, incidentId, "assistant", answer);
        transaction.Commit();
    }

    private SqliteConnection OpenConnection()
    {
        var directory = Path.GetDirectoryName(_databasePath)
            ?? throw new InvalidOperationException("The incident database path is invalid.");
        Directory.CreateDirectory(directory);
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var schema = connection.CreateCommand();
        schema.CommandText = Schema;
        schema.ExecuteNonQuery();
        return connection;
    }

    private static IncidentRecord CreateIncident(AnalysisRequest request, AnalysisResult result)
    {
        var id = request.RequestId.ToString("N");
        return result switch
        {
            RealityCheckResult reality => new IncidentRecord(
                id, request.Mode.ToString(), reality.VideoTitle, reality.VideoUrl, reality.VerdictTitle,
                reality.RealismPercent, reality.Summary, reality.Explanation,
                JsonSerializer.Serialize(new
                {
                    reality.Claims,
                    reality.Sources,
                    reality.WebVerified,
                    reality.EvidenceSearched
                }),
                reality.CompletedAt),
            AiDetectorResult detector => new IncidentRecord(
                id, request.Mode.ToString(), detector.VideoTitle, detector.VideoUrl, detector.VerdictTitle,
                detector.AiLikelihoodPercent, detector.VideoSummary, detector.Explanation,
                JsonSerializer.Serialize(new
                {
                    detector.Findings,
                    detector.Sources,
                    detector.TranscriptSource,
                    detector.AudioAnalyzed,
                    detector.ForensicModelUsed
                }),
                detector.CompletedAt),
            _ => throw new ArgumentOutOfRangeException(nameof(result), "Unsupported analysis result type.")
        };
    }

    private static void AddIncidentParameters(SqliteCommand command, IncidentRecord incident)
    {
        command.Parameters.AddWithValue("@id", incident.Id);
        command.Parameters.AddWithValue("@mode", incident.Mode);
        command.Parameters.AddWithValue("@title", incident.Title);
        command.Parameters.AddWithValue("@source_url", incident.SourceUrl);
        command.Parameters.AddWithValue("@verdict", incident.Verdict);
        command.Parameters.AddWithValue("@score", incident.Score is int score ? score : DBNull.Value);
        command.Parameters.AddWithValue("@summary", incident.Summary);
        command.Parameters.AddWithValue("@explanation", incident.Explanation);
        command.Parameters.AddWithValue("@details_json", incident.DetailsJson);
        command.Parameters.AddWithValue("@created_at", incident.CreatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    }

    private static IncidentRecord ReadIncident(SqliteDataReader reader) => new(
        reader.GetString(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetInt32(5),
        reader.GetString(6),
        reader.GetString(7),
        reader.GetString(8),
        DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture));

    private static void InsertMessage(SqliteConnection connection, SqliteTransaction transaction,
        string incidentId, string role, string content)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO incident_messages (incident_id, role, content, created_at)
            VALUES (@incident_id, @role, @content, @created_at);
            """;
        command.Parameters.AddWithValue("@incident_id", incidentId);
        command.Parameters.AddWithValue("@role", role);
        command.Parameters.AddWithValue("@content", content);
        command.Parameters.AddWithValue("@created_at", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }
}
