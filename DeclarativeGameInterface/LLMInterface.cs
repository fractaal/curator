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

    // private readonly string MODEL = "openai/gpt-3.5-turbo-0125";

    private readonly string DEFAULT_MODEL = "google/gemini-pro";
    private string MODEL = "";
    private float MODEL_TEMPERATURE = 1.0f;
    private string AUX_MODEL = "";
    private float AUX_MODEL_TEMPERATURE = 1.0f;

    private float MODEL_FREQUENCY_PENALTY = 1f;
    private float MODEL_PRESENCE_PENALTY = 1f;
    private float MODEL_REPETITION_PENALTY = 0f;

    private float AUX_MODEL_FREQUENCY_PENALTY = 1f;
    private float AUX_MODEL_PRESENCE_PENALTY = 1f;
    private float AUX_MODEL_REPETITION_PENALTY = 0f;

    private int RESPONSE_MAX_TOKENS = 750;

    private bool SettingsFileMissing = false;

    // private readonly string MODEL = "cohere/command-r";
    // private readonly string MODEL = "lizpreciatior/lzlv-70b-fp16-hf";

    private EventBus Bus;

    private string AccumulatedLLMResponse = "";

    public LLMInterface()
    {
        string paramsFile = Path.Combine(
            Path.GetDirectoryName(OS.GetExecutablePath()),
            "settings.txt"
        );

        if (Config.SettingsFileMissing)
        {
            SettingsFileMissing = true;
            return;
        }

        MODEL = Config.Get("MODEL") ?? DEFAULT_MODEL;
        MODEL = MODEL.Trim();

        AUX_MODEL = Config.Get("AUX_MODEL") ?? DEFAULT_MODEL;
        AUX_MODEL = AUX_MODEL.Trim();

        try
        {
            MODEL_TEMPERATURE = float.Parse(Config.Get("MODEL_TEMPERATURE"));
            AUX_MODEL_TEMPERATURE = float.Parse(Config.Get("AUX_MODEL_TEMPERATURE"));

            MODEL_FREQUENCY_PENALTY = float.Parse(Config.Get("MODEL_FREQUENCY_PENALTY"));
            MODEL_PRESENCE_PENALTY = float.Parse(Config.Get("MODEL_PRESENCE_PENALTY"));
            MODEL_REPETITION_PENALTY = float.Parse(Config.Get("MODEL_REPETITION_PENALTY"));

            AUX_MODEL_FREQUENCY_PENALTY = float.Parse(Config.Get("AUX_MODEL_FREQUENCY_PENALTY"));
            AUX_MODEL_PRESENCE_PENALTY = float.Parse(Config.Get("AUX_MODEL_PRESENCE_PENALTY"));
            AUX_MODEL_REPETITION_PENALTY = float.Parse(Config.Get("AUX_MODEL_REPETITION_PENALTY"));

            RESPONSE_MAX_TOKENS = int.Parse(Config.Get("RESPONSE_MAX_TOKENS"));
        }
        catch (Exception e)
        {
            GD.PrintErr("Failed to parse model parameters in settings.txt: " + e.Message);
        }

        _client.DefaultRequestHeaders.Add("Authorization", "Bearer " + Config.Get("API_KEY"));
        _client.DefaultRequestHeaders.Add("X-Title", "Curator");
    }

    public override void _Ready()
    {
        Bus = EventBus.Get();

        LogManager.UpdateLog(
            "llmModel",
            "[color=\"0000FF\"]Using model [b]" + MODEL + "[/b][/color]"
        );

        if (SettingsFileMissing)
        {
            GetTree()
                .CurrentScene
                .GetNode<RichTextLabel>("CenterContainer/SettingsFileMissingWarning")
                .Visible = true;
        }

        Bus.LLMResponseChunk += (chunk) =>
        {
            AccumulatedLLMResponse += chunk;
        };

        Bus.LLMLastResponseChunk += (chunk) =>
        {
            GD.Print("Last response chunk!");
            Bus.EmitSignal(EventBus.SignalName.LLMFullResponse, AccumulatedLLMResponse);
            AccumulatedLLMResponse = "";
        };
    }

    public void _emitLLMPrompted()
    {
        Bus.EmitSignal(EventBus.SignalName.LLMPrompted);
    }

    public void _emitLLMResponseChunk(string chunk)
    {
        Bus.EmitSignal(EventBus.SignalName.LLMResponseChunk, chunk);
    }

    public void _emitLLMFirstResponseChunk(string chunk)
    {
        Bus.EmitSignal(EventBus.SignalName.LLMFirstResponseChunk, chunk);
    }

    public void _emitLLMLastResponseChunk(string chunk)
    {
        Bus.EmitSignal(EventBus.SignalName.LLMLastResponseChunk, chunk);
    }

    public void _emitCriticalMessage(string message)
    {
        Bus.EmitSignal(EventBus.SignalName.CriticalMessage, message);
    }

    public void EmitLLMPrompted()
    {
        CallDeferred(nameof(_emitLLMPrompted));
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

    public void EmitCriticalMesage(string message)
    {
        CallDeferred(nameof(_emitCriticalMessage), message);
    }

    public void Send(List<Message> messages)
    {
        var thread = new System.Threading.Thread(async () => await DoRequest(messages))
        {
            IsBackground = true
        };

        thread.Start();
        EmitLLMPrompted();
    }

    public async Task<string> SendIsolated(List<Message> messages)
    {
        try
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Content = JsonContent.Create(
                    new
                    {
                        model = AUX_MODEL,
                        messages = messages.ToArray(),
                        temperature = AUX_MODEL_TEMPERATURE,
                        frequency_penalty = AUX_MODEL_FREQUENCY_PENALTY,
                        presence_penalty = AUX_MODEL_PRESENCE_PENALTY,
                        repetition_penalty = AUX_MODEL_REPETITION_PENALTY,
                        max_tokens = RESPONSE_MAX_TOKENS,
                        stop = new string[] { "<END AI TICK>" }
                    }
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

                    if (json.ContainsKey("choices"))
                    {
                        var choices = json["choices"].AsArray();

                        if (choices.Count > 0)
                        {
                            var message = choices[0]["message"]["content"].AsValue().ToString();
                            GD.Print(message);
                            return message;
                        }
                        else
                        {
                            throw new Exception(
                                "No choices in response. Response was: " + responseString
                            );
                        }
                    }
                    else
                    {
                        throw new Exception(
                            "No choices key in response. Response was: " + responseString
                        );
                    }
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
            GD.PrintErr("Failed to send request: " + e.Message + ", " + e.StackTrace);
            if (e.Message.Contains("not known"))
            {
                EmitCriticalMesage("Your internet connection is unstable!");
            }
            return "";
        }
    }

    private async Task DoRequest(List<Message> messages)
    {
        try
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Content = JsonContent.Create(
                    new
                    {
                        temperature = MODEL_TEMPERATURE,
                        stream = true,
                        model = MODEL,
                        messages = messages.ToArray(),
                        frequency_penalty = MODEL_FREQUENCY_PENALTY,
                        presence_penalty = MODEL_PRESENCE_PENALTY,
                        repetition_penalty = MODEL_REPETITION_PENALTY,
                        max_tokens = RESPONSE_MAX_TOKENS,
                        stop = new string[] { "<END AI TICK>" }
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

                                GD.PrintErr("Failed to parse JSON data: " + e.Message);
                                GD.PrintErr("Raw chunk: " + rawChunk);

                                EmitCriticalMesage(
                                    "[color=\"#FF0000\"]LLM didn't respond with the appropriate data schema - most likely flagged/censored. Do other things in-game to nudge it back into responding or restart.[/color]"
                                );
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
            if (e.Message.Contains("not known"))
            {
                EmitCriticalMesage("Your internet connection is unstable!");
            }

            EmitLLMLastResponseChunk("");
        }
    }
}
