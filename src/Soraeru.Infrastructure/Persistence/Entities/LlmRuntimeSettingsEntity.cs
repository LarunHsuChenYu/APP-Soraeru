namespace Soraeru.Infrastructure.Persistence.Entities;

/// <summary>Singleton row (Id=1): optional overrides over appsettings/env.</summary>
public sealed class LlmRuntimeSettingsEntity
{
    public int Id { get; set; } = 1;

    public string? ApiKey { get; set; }

    public string? Model { get; set; }

    public string? BaseUrl { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string? UpdatedByEmail { get; set; }
}

public sealed class LlmUsageEntity
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public string FeatureType { get; set; } = "text_analysis";

    public string Model { get; set; } = "";

    public string Provider { get; set; } = "OpenAICompatible";

    public int? PromptTokens { get; set; }

    public int? CompletionTokens { get; set; }

    public int LatencyMs { get; set; }

    public bool Success { get; set; }

    public string? ErrorCode { get; set; }

    public decimal EstimatedCostNtd { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
