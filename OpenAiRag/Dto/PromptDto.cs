namespace OpenAiRag.Dto;

public class PromptDto
{
    public string PromptMessage { get; set; }
    public string? ThreadId { get; set; } = string.Empty;
}
