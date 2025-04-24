#pragma warning disable OPENAI001
using Microsoft.AspNetCore.Mvc;
using OpenAiRag.Dto;
using OpenAI.Assistants;
using OpenAI;

namespace OpenAiRag.Controllers;

[Route("api/[controller]/[action]")]
[ApiController]
public class AgentController : ControllerBase
{
    private const string _assistantId = "";
    private const string _apiKey = "";

    private readonly OpenAIClient _client;
    private readonly AssistantClient _assistantClient;

    public AgentController()
    {
        _client = new OpenAIClient(_apiKey);
        _assistantClient = _client.GetAssistantClient();
    }

    [HttpPost]
    public async Task<IActionResult> Chat(PromptDto prompt)
    {
        try
        {
            var (response, threadId) = await GetResponseAsync(prompt.PromptMessage, prompt.ThreadId);
            return Ok(new { reply = response, threadId });
        }
        catch (Exception ex)
        {
            return StatusCode(500);
        }
    }

    private async Task<(string Response, string ThreadId)> GetResponseAsync(string message, string? threadId = null)
    {
        System.ClientModel.ClientResult<ThreadRun> threadRun;

        if (!string.IsNullOrEmpty(threadId))
            threadRun = await _assistantClient.CreateRunAsync(threadId, _assistantId,
                        new RunCreationOptions
                        {
                            AdditionalMessages = { message },
                        });
        else
            threadRun = await _assistantClient.CreateThreadAndRunAsync(_assistantId, new ThreadCreationOptions
            {
                InitialMessages = { message }
            });

        while (!threadRun.Value.Status.IsTerminal)
        {
            await Task.Delay(500);
            threadRun = _assistantClient.GetRun(threadRun.Value.ThreadId, threadRun.Value.Id);
        }

        var messages = _assistantClient.GetMessages(threadRun.Value.ThreadId);
        var messageItem = messages.First();

        string responseText = "";
        foreach (var content in messageItem.Content)
        {
            var text = content.Text;
            foreach (var annotation in content.TextAnnotations)
                text = text.Replace(annotation.TextToReplace, "");
            
            responseText += text;
        }

        return (responseText, threadRun.Value.ThreadId);
    }
}
