#pragma warning disable OPENAI001
using Microsoft.AspNetCore.Mvc;
using OpenAiRag.Dto;
using OpenAI.Assistants;
using OpenAI;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace OpenAiRag.Controllers;

[Route("api/[controller]/[action]")]
[ApiController]
public class AgentController : ControllerBase
{
    private const string _assistantId = "OpenAiAssistantId";
    private const string _apiKey = "AssistantApiKey";

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

    [HttpPost]
    public async Task ChatStream(PromptDto prompt)
    {
        try
        {
            var (responseStream, threadId) = await GetStreamResponseAsync(prompt.PromptMessage, prompt.ThreadId);

            Response.Headers["Content-Type"] = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";

            await foreach (var chunk in responseStream)
            {
                await Response.WriteAsync($"response: {JsonSerializer.Serialize(new { reply = chunk, threadId })}\n\n");
                await Response.Body.FlushAsync();
            }
        }
        catch (Exception ex)
        {
            await Response.WriteAsync($"response: {JsonSerializer.Serialize(new { error = ex.Message })}\n\n");
            await Response.Body.FlushAsync();
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
            if (threadRun.Value.Status == RunStatus.RequiresAction)
                foreach (var action in threadRun.Value.RequiredActions)
                    switch (action.FunctionName)
                    {
                        case nameof(GetWeatherInCelcius):
                            using (JsonDocument argumentsJson = JsonDocument.Parse(action.FunctionArguments))
                            {
                                bool hasCityArgument = argumentsJson.RootElement.TryGetProperty("city", out JsonElement city);

                                if (!hasCityArgument)
                                    throw new ArgumentNullException(nameof(city), "City is required.");

                                var weather = await GetWeatherInCelcius(city.ToString());

                                threadRun = await _assistantClient.SubmitToolOutputsToRunAsync(threadRun.Value.ThreadId, threadRun.Value.Id,
                                [
                                    new ToolOutput
                                    {
                                        ToolCallId = action.ToolCallId,
                                        Output = weather,
                                    }
                                ]);
                            }
                            break;
                    }

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


    private async Task<(IAsyncEnumerable<string> ResponseStream, string ThreadId)> GetStreamResponseAsync(string message, string? threadId = null)
    {
        System.ClientModel.AsyncCollectionResult<StreamingUpdate> threadRun;
        var channel = Channel.CreateUnbounded<string>();
        string capturedThreadId = threadId ?? string.Empty;
        var threadIdCaptured = new TaskCompletionSource<bool>();

        if (!string.IsNullOrEmpty(threadId))
            threadRun = _assistantClient.CreateRunStreamingAsync(threadId, _assistantId,
                new RunCreationOptions
                {
                    AdditionalMessages = { message },
                });
        else
            threadRun = _assistantClient.CreateThreadAndRunStreamingAsync(_assistantId, new ThreadCreationOptions
            {
                InitialMessages = { message }
            });

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var streamingUpdate in threadRun)
                {
                    if (string.IsNullOrWhiteSpace(capturedThreadId))
                        capturedThreadId = streamingUpdate switch
                        {
                            ThreadUpdate tu => tu.Id,
                            RunUpdate ru => ru.Value.ThreadId,
                            RunStepUpdate rsu => rsu.Value.ThreadId,
                            _ => capturedThreadId
                        };

                    if (!string.IsNullOrWhiteSpace(capturedThreadId))
                        threadIdCaptured.TrySetResult(true);

                    if (streamingUpdate.UpdateKind == StreamingUpdateReason.RunRequiresAction)
                        if (streamingUpdate is RequiredActionUpdate action)
                            switch (action.FunctionName)
                            {
                                case nameof(GetWeatherInCelcius):
                                    using (JsonDocument argumentsJson = JsonDocument.Parse(action.FunctionArguments))
                                    {
                                        bool hasCityArgument = argumentsJson.RootElement.TryGetProperty("city", out JsonElement city);

                                        if (!hasCityArgument)
                                            throw new ArgumentNullException(nameof(city), "City is required.");

                                        var weather = await GetWeatherInCelcius(city.ToString());

                                        threadRun = _assistantClient.SubmitToolOutputsToRunStreamingAsync(
                                            action.Value.ThreadId,
                                            action.Value.Id,
                                            [new ToolOutput { ToolCallId = action.ToolCallId, Output = weather }]
                                        );

                                        await foreach (var actionUpdate in threadRun)
                                            if (actionUpdate is MessageContentUpdate contentUpdate)
                                            {
                                                string cleanedText = Regex.Replace(contentUpdate.Text ?? "", @"【\d+:\d+†source】", "");
                                                await channel.Writer.WriteAsync(cleanedText);
                                            }
                                    }
                                    break;
                            }
                    else if (streamingUpdate is MessageContentUpdate contentUpdate)
                    {
                        string cleanedText = Regex.Replace(contentUpdate.Text ?? "", @"【\d+:\d+†source】", "");
                        await channel.Writer.WriteAsync(cleanedText);
                    }
                }
            }
            catch (Exception ex)
            {
                threadIdCaptured.TrySetException(ex);
            }
            finally
            {
                channel.Writer.Complete();
            }
        });

        await Task.WhenAny(threadIdCaptured.Task, Task.Delay(5000));
        return (channel.Reader.ReadAllAsync(), capturedThreadId);
    }


    private Task<string> GetWeatherInCelcius(string city)
        => Task.FromResult(city switch
        {
            "Karachi" => "20C",
            "Lahore" => "30C",
            "Islamabad" => "25C",
            _ => "28C",
        });
}
