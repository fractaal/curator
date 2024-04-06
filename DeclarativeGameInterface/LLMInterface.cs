using System;
using System.Collections.Generic; // For List
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

public struct Message
{
    public string role { get; set; }
    public string content { get; set; }
}

public partial class LLMInterface : Node
{
    [Export]
    public Control DebugUIContainer;

    private readonly System.Net.Http.HttpClient _client = new System.Net.Http.HttpClient();
    private readonly string url = "https://openrouter.ai/api/v1/chat/completions";
    private readonly string API_KEY =
        "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";

    private readonly string MODEL = "openai/gpt-3.5-turbo-0125";

    // private readonly string MODEL = "google/gemini-pro";

    // private readonly string MODEL = "cohere/command-r";
    // private readonly string MODEL = "lizpreciatior/lzlv-70b-fp16-hf";

    public LLMInterface()
    {
        _client.DefaultRequestHeaders.Add("Authorization", "Bearer " + API_KEY);
        _client.DefaultRequestHeaders.Add("X-Title", "Curator");
    }

    public override void _Ready()
    {
        // Send("Respond with the 'the quick brown fox' test message 5 times, if understood");

        LogManager.UpdateLog(
            "llmModel",
            "[color=\"0000FF\"]Using model [b]" + MODEL + "[/b][/color]"
        );
    }

    public void _emitLLMResponseChunk(string chunk)
    {
        EventBus.Get().EmitSignal(EventBus.SignalName.LLMResponseChunk, chunk);
    }

    public void _emitLLMFirstResponseChunk(string chunk)
    {
        EventBus.Get().EmitSignal(EventBus.SignalName.LLMFirstResponseChunk, chunk);
    }

    public void _emitLLMLastResponseChunk(string chunk)
    {
        EventBus.Get().EmitSignal(EventBus.SignalName.LLMLastResponseChunk, chunk);
    }

    public void EmitLLMResponseChunk(string chunk)
    {
        CallDeferred(nameof(_emitLLMResponseChunk), chunk);
    }

    public void EmitLLMFirstResponseChunk(string chunk)
    {
        CallDeferred(nameof(_emitLLMFirstResponseChunk), chunk);
    }

    public void EmitLLMLastResponseChunk(string chunk)
    {
        CallDeferred(nameof(_emitLLMLastResponseChunk), chunk);
    }

    public void Send(List<Message> messages)
    {
        var thread = new System.Threading.Thread(async () => await DoRequest(messages))
        {
            IsBackground = true
        };

        thread.Start();
    }

    public async Task<string> SendIsolated(List<Message> messages)
    {
        try
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Content = JsonContent.Create(
                    new { model = MODEL, messages = messages.ToArray() }
                );

                var response = await _client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead
                );

                if (response.IsSuccessStatusCode)
                {
                    // Assuming you want to return the response content as a string
                    var responseString = await response.Content.ReadAsStringAsync();

                    var json = JsonNode.Parse(responseString).AsObject();
                    var message = json["choices"]
                        .AsArray()[0]["message"]["content"]
                        .AsValue()
                        .ToString();
                    GD.Print(message);
                    return message;
                }
                else
                {
                    // Handle non-success status codes as needed
                    return "Error: " + response.StatusCode;
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr("Failed to send request: " + e.Message);
            return "";
        }
    }

    private async Task DoRequest(List<Message> messages)
    {
        EventBus bus = EventBus.Get();

        try
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Content = JsonContent.Create(
                    new
                    {
                        stream = true,
                        model = MODEL,
                        messages = messages.ToArray()
                    }
                );

                var response = await _client
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var stream = await response.Content.ReadAsStreamAsync();

                bool hasFirstResponse = false;

                using (var reader = new StreamReader(stream))
                {
                    string uiOut = "[color=\"00FF00\"][b]LLM RESPONSE:[/b][/color] ";
                    string lastChunk = "";

                    while (!reader.EndOfStream)
                    {
                        var rawChunk = await reader.ReadLineAsync();
                        var chunk = "";
                        // Safely call back to the main thread to handle the line

                        // Take out the first 6 characters "data: "
                        if (rawChunk.StartsWith("data: [DONE]"))
                        {
                            // uiOut += "\n[color=\"#00FF00\"]- LLM PROCESSING DONE -[/color]";
                        }
                        else if (rawChunk.StartsWith("data: "))
                        {
                            rawChunk = rawChunk.Substring(6);
                            try
                            {
                                JsonObject json = JsonNode.Parse(rawChunk).AsObject();
                                // {"choices": [{"index": 0, "delta": {"role": "assistant", "content": <TARGET>}}]}
                                JsonArray choices = json["choices"].AsArray();
                                JsonObject delta = choices[0]["delta"].AsObject();
                                string target = delta["content"].AsValue().ToString();

                                chunk = target;
                                uiOut += target;
                            }
                            catch (Exception e)
                            {
                                // uiOut +=
                                //     "\n[b][color=\"#FF0000\"]<ERROR PARSING JSON DATA:[/color][/b] "
                                //     + e.Message
                                //     + "\n";
                            }
                        }
                        else if (rawChunk.Contains("OPENROUTER PROCESSING"))
                        {
                            // uiOut += "\n[color=\"#FFA500\"]- LLM PROCESSING... -[/color]";
                        }
                        else if (rawChunk.Contains("[DONE]")) { }
                        else
                        {
                            uiOut += rawChunk;
                        }

                        if (!hasFirstResponse)
                        {
                            hasFirstResponse = true;
                            EmitLLMFirstResponseChunk(chunk);
                        }

                        lastChunk = chunk;

                        EmitLLMResponseChunk(chunk);
                    }

                    EmitLLMLastResponseChunk(lastChunk);
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr("Failed to send request: " + e.Message);
            EmitLLMLastResponseChunk("");
        }
    }
}
