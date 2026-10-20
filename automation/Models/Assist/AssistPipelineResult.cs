namespace Automation.Models.Assist;

/// <summary>
/// The outcome of running audio through a Home Assistant Assist pipeline (speech-to-text followed by intent
/// recognition).
/// </summary>
public record AssistPipelineResult
{
    /// <summary>Whether the pipeline ran to completion without an error.</summary>
    public bool Success { get; init; }

    /// <summary>What Home Assistant understood the audio to say, from the speech-to-text stage.</summary>
    public string? Transcript { get; init; }

    /// <summary>What the conversation agent replied, in plain text.</summary>
    public string? ResponseText { get; init; }

    /// <summary>When <see cref="Success"/> is <c>false</c>, why the pipeline run failed.</summary>
    public string? Error { get; init; }
}
