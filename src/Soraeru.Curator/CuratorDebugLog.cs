using System.Text;
using System.Text.Json;

namespace Soraeru.Curator;

/// <summary>Debug-mode NDJSON logger (session 7ea34e). Do not log secrets.</summary>
internal static class CuratorDebugLog
{
    private const string SessionId = "7ea34e";
    private const string LogPath = @"d:\VS\Soraeru\debug-7ea34e.log";
    private const string IngestUrl = "http://127.0.0.1:7562/ingest/1b1ca3c8-64a6-4444-867b-133ad589f4fc";

    public static async Task WriteAsync(string hypothesisId, string location, string message, object data)
    {
        try
        {
            var payload = new Dictionary<string, object?>
            {
                ["sessionId"] = SessionId,
                ["hypothesisId"] = hypothesisId,
                ["location"] = location,
                ["message"] = message,
                ["data"] = data,
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ["runId"] = "pre-fix"
            };
            var line = JsonSerializer.Serialize(payload) + Environment.NewLine;
            await File.AppendAllTextAsync(LogPath, line);

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                using var content = new StringContent(line, Encoding.UTF8, "application/json");
                content.Headers.TryAddWithoutValidation("X-Debug-Session-Id", SessionId);
                await client.PostAsync(IngestUrl, content);
            }
            catch
            {
                // ignore ingest failures
            }
        }
        catch
        {
            // ignore file failures
        }
    }
}
