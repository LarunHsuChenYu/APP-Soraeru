using System.Text.Json;

namespace Soraeru.Curator.Diagnostics;

/// <summary>Debug-mode NDJSON logger (session 50837b). Do not log secrets.</summary>
internal static class AgentDebugLog
{
    private const string SessionId = "50837b";
    private const string LocalLogPath = @"D:\VS\Soraeru\debug-50837b.log";
    private const string IngestUrl = "http://127.0.0.1:7562/ingest/1b1ca3c8-64a6-4444-867b-133ad589f4fc";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMilliseconds(500) };
    private static readonly object Gate = new();
    private static string? _lastSummary;

    public static string? LastSummary
    {
        get { lock (Gate) return _lastSummary; }
    }

    public static void Write(string hypothesisId, string location, string message, object? data = null)
    {
        // #region agent log
        try
        {
            var payload = new Dictionary<string, object?>
            {
                ["sessionId"] = SessionId,
                ["runId"] = "pre-fix",
                ["hypothesisId"] = hypothesisId,
                ["location"] = location,
                ["message"] = message,
                ["data"] = data,
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            var line = JsonSerializer.Serialize(payload);
            lock (Gate)
            {
                _lastSummary = $"{hypothesisId}|{location}|{message}|{JsonSerializer.Serialize(data)}";
            }

            Console.WriteLine("AGENT_DEBUG " + line);

            TryAppend(LocalLogPath, line);
            TryAppend("/tmp/debug-50837b.log", line);
            TryAppend(Path.Combine(AppContext.BaseDirectory, "debug-50837b.log"), line);

            _ = Http.PostAsJsonAsync(IngestUrl, payload);
        }
        catch
        {
            // ignore debug sink failures
        }
        // #endregion
    }

    private static void TryAppend(string path, string line)
    {
        try
        {
            File.AppendAllText(path, line + Environment.NewLine);
        }
        catch
        {
            // path may be unwritable on Railway / non-Windows
        }
    }

    private static Task PostAsJsonAsync(this HttpClient http, string url, object payload) =>
        http.PostAsync(
            url,
            new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json"));
}
