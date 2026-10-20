using Automation.Models.Assist;

namespace Automation.Interfaces;

/// <summary>
/// Runs audio through a named Home Assistant Assist pipeline (speech-to-text followed by intent recognition).
/// NetDaemon's own Home Assistant connection cannot do this: it only supports simple request/response
/// commands, while a pipeline run is a streaming command that also needs binary audio frames.
/// </summary>
public interface IAssistPipelineClient
{
    /// <summary>
    /// Runs audio through the named pipeline's speech-to-text and intent stages.
    /// </summary>
    /// <param name="pipelineName">The name of the Assist pipeline to use, e.g. "Home Assistant Cloud".</param>
    /// <param name="pcm16">Mono, 16-bit little-endian PCM audio at <paramref name="sampleRate"/>.</param>
    /// <param name="sampleRate">The sample rate of <paramref name="pcm16"/>, in Hz.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    Task<AssistPipelineResult> RunSpeechToIntentAsync(string pipelineName, byte[] pcm16, int sampleRate, CancellationToken cancellationToken);
}
