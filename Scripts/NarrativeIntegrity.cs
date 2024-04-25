using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class NarrativeIntegrity : Node
{
    private LLMInterface Interface;
    private Node GhostData;

    private string SanitizerPrompt = FileAccess
        .Open(
            "res://DeclarativeGameInterface/prompts/SanitizerPrompt.txt",
            FileAccess.ModeFlags.Read
        )
        .GetAsText();

    private EventBus Bus;

    private List<string> SuspiciousSubstrings = new List<string>()
    {
        "=",
        "*",
        ";",
        ":",
        "[",
        "]",
        "/",
        "\\",
        "+",
        "-",
        "<",
        ">",
        "volume",
        "filter",
        "distorted",
        "voice",
        "room",
        "resolve",
        "target",
        "arg",
        "param",
        "whisper",
        "message",
        "roleplay",
        "universe",
        "language",
        "vague",
        "nonsensical",
        "context",
        "natural"
    };

    public override void _Ready()
    {
        Interface = GetNode<LLMInterface>("/root/LLMInterface");
        GhostData = GetNode<Node>("/root/GhostData");
        Bus = EventBus.Get();

        GD.Print(SanitizerPrompt);
    }

    public async Task<string> CheckIntegrityForAudio(string message, string action)
    {
        var rooms = GetTree().GetNodesInGroup("rooms").Select(room => room.Name.ToString());
        var ghostTypes = GhostData.Call("GetGhostTypes").AsStringArray();

        var substrings = new List<string>(SuspiciousSubstrings);
        substrings.AddRange(rooms);
        substrings.AddRange(ghostTypes);

        if (message == null)
        {
            GD.PushWarning("Integrity check received null message.");
            return "";
        }

        if (message.Length < 1)
        {
            GD.PushWarning("Integrity check received empty message.");
            return "";
        }

        List<string> problemSubstrings = new List<string>();

        foreach (string substring in substrings)
        {
            if (message.ToLower().Contains(substring.ToLower()))
            {
                GD.PushWarning(
                    $"Message contains suspicious substring: {substring} - amending message."
                );

                problemSubstrings.Add(substring);
            }
        }

        if (problemSubstrings.Count > 0)
        {
            var sanitized = await Interface.SendIsolated(
                new List<Message>()
                {
                    new() { content = SanitizerPrompt, role = "system" },
                    new()
                    {
                        content = "\"I see you.\", voice=distorted, filter=low, distant.",
                        role = "user"
                    },
                    new()
                    {
                        content =
                            "VERDICT: Hallucinated configuration parameters. Text should *just be natural language.*\nI see you.",
                        role = "assistant"
                    },
                    new() { content = "Whispered message", role = "user", },
                    new()
                    {
                        content =
                            "VERDICT: Vague, roleplaying, descriptive prose - nonsensical or out of context in the context of in-universe speech.",
                        role = "assistant"
                    },
                    new() { content = "Low, growling, whispered message", role = "user", },
                    new()
                    {
                        content =
                            "VERDICT: Vague, roleplaying, descriptive prose - nonsensical or out of context in the context of in-universe speech.",
                        role = "assistant"
                    },
                    new() { content = "*Laughing.* You think you can catch me?", role = "user", },
                    new()
                    {
                        content =
                            "VERDICT: Roleplaying - nonsensical or out of context in the context of in-universe speech.\nYou think you can catch me?",
                        role = "assistant"
                    },
                    new() { content = "Mark Walker", role = "user" },
                    new() { content = "Mark Walker", role = "assistant" },
                    new() { content = "My name? David Requinton.", role = "user" },
                    new() { content = "My name? David Requinton.", role = "assistant" },
                    new() { content = "I died in this room.", role = "user", },
                    new() { content = "I died in this room.", role = "assistant", },
                    new() { content = "I died 200 years ago.", role = "user", },
                    new() { content = "I died 200 years ago.", role = "assistant", },
                    new() { content = "I am a demon.", role = "user", },
                    new()
                    {
                        content =
                            "VERDICT: **Divulged ghost type**! Breaks narrative integrity by revealing ghost type.\nHow pitiful.",
                        role = "assistant"
                    },
                    new() { content = "Do you feel my gaze upon you?", role = "user", },
                    new() { content = "Do you feel my gaze upon you?", role = "assistant", },
                    new() { content = message, role = "user" }
                }
            );

            string reason = "";
            string sanitizedMessage = "";

            foreach (string line in sanitized.Split("\n"))
            {
                if (line.Contains("VERDICT"))
                {
                    reason = line.Split(":")[1].Trim();
                }
                else
                {
                    sanitizedMessage += line + "\n";
                }
            }

            if (reason == "")
            {
                GD.PushWarning("Sanitizer did not return a verdict.");
                return sanitizedMessage;
            }

            if (sanitizedMessage == "")
            {
                Bus.EmitSignal(
                    EventBus.SignalName.SystemFeedback,
                    $"NARRATIVE INTEGRITY FAILURE: During {action}, you generated -- \"{message}\" -- as output."
                        + $"This is not allowed, because: **{reason}.** Your message has been completely removed."
                        + "To prevent future removals, abide by narrative integrity, and respect the game objective."
                );
            }
            else
            {
                Bus.EmitSignal(
                    EventBus.SignalName.SystemFeedback,
                    $"NARRATIVE INTEGRITY FAILURE: During {action}, you generated -- \"{message}\" -- as output."
                        + $"This is not allowed, because: **{reason}.** Your message has been amended to -- \"{sanitizedMessage}\" -- "
                        + "Please take note of this in the future and abide by narrative integrity, the example of the amended message,"
                        + "and respect the game objective."
                );
            }

            return sanitizedMessage;
        }

        return message;
    }
}
