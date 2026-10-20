namespace Automation.Models.WhatsApp;

/// <summary>
/// One exchange in a WhatsApp conversation: what the person said (typed, or transcribed from a voice note)
/// and what the assistant replied. Kept per sender so follow-up questions ("wat vroeg ik net") have context.
/// </summary>
public record WhatsAppConversationTurn
{
    public string UserMessage { get; init; } = string.Empty;
    public string AssistantReply { get; init; } = string.Empty;
}
