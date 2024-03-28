using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;
using Godot;

public partial class Prompter : Node
{
    private string SYSTEM_PROMPT = "";
    private List<string> llmResponses = new List<string>();
    private string data = "";

    private string accumulatedLLMResponse = "";

    private EventBus bus;
    private LLMInterface llmInterface;

    private RichTextLabel promptDebugUI;

    public Prompter()
    {
        var file = FileAccess.Open(
            "res://DeclarativeGameInterface/prompts/Main.txt",
            FileAccess.ModeFlags.Read
        );
        SYSTEM_PROMPT = file.GetAsText();
    }

    public override void _Ready()
    {
        bus = GetNode<EventBus>("/root/EventBus");
        llmInterface = GetNode<LLMInterface>("/root/LLMInterface");

        promptDebugUI = GetNode<RichTextLabel>("/root/Node3d/PromptDebug");

        bus.GameDataRead += (data) =>
        {
            string previousResponses = llmResponses
                .TakeLast(5)
                .Aggregate("", (acc, response) => acc + response + "\n");

            if (previousResponses.Length == 0)
            {
                previousResponses = "No previous responses\n";
            }

            string prompt =
                // "-- PREVIOUS RESPONSES --\n"
                // + previousResponses.Trim()
                "" + "\n" + data + "\n -- YOUR RESPONSE? -- \n\n";

            List<Message> messages = new List<Message>
            {
                new Message { role = "user", content = SYSTEM_PROMPT },
                new Message
                {
                    role = "user",
                    content =
                        prompt
                        + "\n\n[Reminder to maintain strict syntax for the actions you want to perform -- they won't be recognized otherwise!]\n\n"
                }
            };

            promptDebugUI.Text = messages.Aggregate(
                "",
                (acc, message) =>
                    acc
                    + "\n [color=\"#ff0000\"][b]"
                    + message.role
                    + "[/b][/color]: "
                    + message.content
                    + "\n"
            );

            llmInterface.Send(messages);
        };

        bus.LLMResponseChunk += (chunk) =>
        {
            accumulatedLLMResponse += chunk;
        };

        bus.LLMLastResponseChunk += (chunk) =>
        {
            llmResponses.Add(accumulatedLLMResponse);
            accumulatedLLMResponse = "";
        };
    }
}
