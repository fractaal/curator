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
        "volume",
        "filter",
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
                    new() { content = message, role = "user" }
                }
            );

            Bus.EmitSignal(
                EventBus.SignalName.SystemFeedback,
                $"NARRATIVE INTEGRITY FAILURE: During {action}, you generated a message - \"{message}\" containing {problemSubstrings.Aggregate("", (acc, curr) => acc + curr + ", ").Trim()}. Symbols/phrases/straight room IDs/ghost types that break the narrative are not allowed in {action}. You need to maintain narrative-prose context during {action}. Your message has been amended to {sanitized}. Please follow the example of the amended message"
            );

            return sanitized;
        }

        return message;
    }
}
