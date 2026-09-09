namespace Soraeru.Application.Abstractions.Llm;

/// <summary>
/// Single Word Analysis Agent boundary (MVP: one LLM call, JSON schema output).
/// </summary>
public interface IWordAnalysisAgent
{
    Task<WordAnalysisAgentOutcome> AnalyzeAsync(
        WordAnalysisAgentRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record WordAnalysisAgentRequest(
    string Text,
    string SourceLanguage,
    string MemoryLanguage,
    string NotationPreference,
    /// <summary>
    /// When true, agent must only produce meaning / reading / language fields (no empty-ear mnemonics).
    /// Used on verified-hit path (ADR-0001).
    /// </summary>
    bool SkipMnemonics = false,
    /// <summary>Optional actor for LlmUsage attribution.</summary>
    Guid? ActorUserId = null);

/// <summary>
/// Either a valid payload or a model-declared unanalyzable / transport failure.
/// </summary>
public abstract record WordAnalysisAgentOutcome;

public sealed record WordAnalysisAgentSuccess(WordAnalysisPayload Payload) : WordAnalysisAgentOutcome;

public sealed record WordAnalysisAgentFailure(string Code, string Message) : WordAnalysisAgentOutcome;

public sealed record WordAnalysisPayload(
    string SourceText,
    string NormalizedText,
    string SourceLanguage,
    string LanguageDisplayName,
    string Meaning,
    string ReadingText,
    IReadOnlyList<WordAnalysisMnemonic> Mnemonics,
    string Notice);

public sealed record WordAnalysisMnemonic(
    string DisplayText,
    string NotationType,
    string NotationText,
    string Explanation);
