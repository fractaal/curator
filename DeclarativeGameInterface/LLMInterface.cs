using System;
using System.Collections.Generic; // For List
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

public partial class LLMInterface : Node
{
    [Export]
    public Control DebugUIContainer;

    private readonly System.Net.Http.HttpClient _client = new System.Net.Http.HttpClient();
    private readonly string url = "https://openrouter.ai/api/v1/chat/completions";
    private readonly string API_KEY =
        "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";

    private readonly string MODEL = "google/gemma-7b-it:free";

    // List to store the conversation context
    private List<object> _messages = new List<object>();

    public static event Action<string, string> LogUpdated;
    public static event Action<string> LLMResponseChunk;

    public LLMInterface()
    {
        _client.DefaultRequestHeaders.Add("Authorization", "Bearer " + API_KEY);
        _client.DefaultRequestHeaders.Add("X-Title", "Curator");

        // LogUpdated += (id, message) =>
        // {
        //     CallDeferred(nameof(_onLogUpdated), id, message);
        // };
    }

    private void _ready()
    {
        Send("Respond with the 'the quick brown fox' test message 5 times, if understood");

        LogManager.UpdateLog("start", "[color=\"0000FF\"]Using model [b]" + MODEL + "[/b][/color]");
    }

    public void Send(string message)
    {
        _messages.Add(new { role = "user", content = message });

        // Start the request in a background thread
        var thread = new System.Threading.Thread(async () => await DoRequest(message))
        {
            IsBackground = true
        };
        thread.Start();
    }

    private async Task DoRequest(string message)
    {
        var id = _messages.Count.ToString();

        LogManager.UpdateLog(id + "game", "[b]GAME:[/b] " + message);

        LogManager.UpdateLog(
            id + "response",
            "[color=\"#FFA500\"]- WAITING FOR LLM RESPONSE -[/color]"
        );

        try
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Content = JsonContent.Create(
                    new
                    {
                        stream = true,
                        model = MODEL,
                        messages = _messages.ToArray()
                    }
                );

                var response = await _client
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var stream = await response.Content.ReadAsStreamAsync();
                using (var reader = new StreamReader(stream))
                {
                    string uiOut = "[color=\"00FF00\"][b]LLM RESPONSE:[/b][/color] ";

                    LogManager.UpdateLog(id + "response", uiOut);

                    while (!reader.EndOfStream)
                    {
                        var chunk = await reader.ReadLineAsync();
                        // Safely call back to the main thread to handle the line

                        GD.Print(chunk);

                        // Take out the first 6 characters "data: "
                        if (chunk.StartsWith("data: [DONE]"))
                        {
                            uiOut += "\n[color=\"#00FF00\"]- LLM PROCESSING DONE -[/color]";
                        }
                        else if (chunk.StartsWith("data: "))
                        {
                            chunk = chunk.Substring(6);
                            try
                            {
                                JsonObject json = JsonNode.Parse(chunk).AsObject();
                                // {"choices": [{"index": 0, "delta": {"role": "assistant", "content": <TARGET>}}]}
                                JsonArray choices = json["choices"].AsArray();
                                JsonObject delta = choices[0]["delta"].AsObject();
                                string target = delta["content"].AsValue().ToString();

                                uiOut += target;
                            }
                            catch (Exception e)
                            {
                                uiOut +=
                                    "\n[b][color=\"#FF0000\"]<ERROR PARSING JSON DATA:[/color][/b] "
                                    + e.Message
                                    + "\n";
                            }
                        }
                        else if (chunk.Contains("OPENROUTER PROCESSING"))
                        {
                            uiOut += "\n[color=\"#FFA500\"]- LLM PROCESSING... -[/color]";
                        }
                        else if (chunk.Contains("[DONE]")) { }
                        else
                        {
                            uiOut += chunk;
                        }

                        LLMResponseChunk?.Invoke(chunk);
                        LogManager.UpdateLog(id + "response", uiOut);
                    }

                    // Add the response to the context
                    _messages.Add(new { role = "assistant", content = uiOut });
                    LogManager.UpdateLog(
                        id + "addedToContext",
                        "[b][color=\"#FF0000\"]<ADDED TO LOCAL CONTEXT.>[/color][/b] "
                    );
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr("Failed to send request: " + e.Message);
        }
    }
}
