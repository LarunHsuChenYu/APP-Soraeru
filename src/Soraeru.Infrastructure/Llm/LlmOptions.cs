namespace Soraeru.Infrastructure.Llm;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>OpenAICompatible (default). Uses Chat Completions JSON.</summary>
    public string Provider { get; set; } = "OpenAICompatible";

    /// <summary>API key used only for one-time SQLite seed. Request path reads DB only.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Model used only for one-time SQLite seed. Request path reads DB only.</summary>
    public string Model { get; set; } = "gemini-3.6-flash";

    /// <summary>Base URL used only for one-time SQLite seed. Request path reads DB only.</summary>
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai";

    public int TimeoutSeconds { get; set; } = 60;
}
