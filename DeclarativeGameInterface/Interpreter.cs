using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;

public struct Command
{
    public string Verb;
    public List<string> Arguments;
}

public partial class Interpreter : Node
{
    private Node3D player = null;
    private Node3D ghost = null;

    private TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;

    private HashSet<string> AcknowledgedCommandsSet = new();

    public override void _Ready()
    {
        LogManager.UpdateLog("llmResponse", "");

        player = (Node3D)GetTree().CurrentScene.FindChild("Player");
        ghost = (Node3D)GetTree().CurrentScene.FindChild("Ghost");

        GD.Print("Initialized interpreter with values player: ", player, " ghost: ", ghost);

        EventBus bus = EventBus.Get();

        bus.LLMFirstResponseChunk += (chunk) =>
        {
            _fullLLMResponse = "";
            LogManager.UpdateLog("llmResponse", "");
        };

        bus.LLMResponseChunk += (chunk) =>
        {
            Interpret(chunk);
        };

        bus.ObjectInteractionAcknowledged += (verb, objectType, target) =>
        {
            if (AcknowledgedCommandsSet.Contains(verb + "/" + objectType + "/" + target)) // This is a hack to prevent double logging
            {
                bus.EmitSignal(
                    EventBus.SignalName.NotableEventOccurred,
                    $"Ghost interacted - {verb}{textInfo.ToTitleCase(objectType)}({target})"
                );
            }

            AcknowledgedCommandsSet.Remove(verb + "/" + objectType + "/" + target);
        };

        bus.LLMLastResponseChunk += (_chunk) =>
        {
            foreach (string acknowledgedCommand in AcknowledgedCommandsSet)
            {
                var split = acknowledgedCommand.Split("/");
                var verb = split[0];
                var objectType = split[1];
                var target = split[2];

                bus.EmitSignal(
                    EventBus.SignalName.SystemFeedback,
                    $"ERROR: Trying to {verb} {objectType} in/at {target} FAILED.\nThe object doesn't exist in the room.\nRefer to the available objects per room in ROOM INFORMATION."
                );
            }

            AcknowledgedCommandsSet.Clear();
        };
    }

    private List<string> objectInteractionVerbPrefixes =
        new()
        {
            "flicker",
            "explode",
            "restore",
            "turnOff",
            "turnOn",
            "playFreakyMusicOn",
            "stop",
            "open",
            "close",
            "unlock",
            "lock",
        };

    private List<string> ghostActionVerbs =
        new()
        {
            "moveAsGhost",
            "moveToAsGhost",
            "speakAsGhost",
            "speakAsSpiritBox",
            "manifest",
            "appear",
            "disappear"
        };

    private string accumulatedText = "";

    private string _fullLLMResponse = "";

    private int countRecognized = 0;

    public List<Command> Parse(string chunk)
    {
        // Matches stuff like verb(), verb(arg1), verb(arg1, arg2)...
        var pattern = @"\w+\([^)]*\)";

        accumulatedText += chunk;

        var matches = Regex.Matches(accumulatedText, pattern);

        accumulatedText = Regex.Replace(
            accumulatedText,
            @"(?<!\])\w+\([^)]*\)(?!\[)",
            match => $""
        );

        if (matches.Count() > 0)
        {
            var recognizedCommands = new List<Command>();

            foreach (Match match in matches)
            {
                EventBus
                    .Get()
                    .EmitSignal(EventBus.SignalName.InterpreterCommandRecognized, match.Value);

                var value = match.Value;
                var separatedString = value.Split("(");
                var verb = separatedString[0];

                var argumentString = separatedString[1].Substring(0, separatedString[1].Length - 1);
                var arguments = argumentString
                    .Split(",")
                    .Select(argument => argument.Trim())
                    .ToList();

                recognizedCommands.Add(new Command { Verb = verb, Arguments = arguments });
            }

            return recognizedCommands;
        }
        else
        {
            return new() { };
        }
    }

    public void Interpret(string chunk)
    {
        EventBus bus = EventBus.Get();

        var pattern = @"\w+\([^)]*\)";

        _fullLLMResponse += chunk;
        _fullLLMResponse = Regex.Replace(
            _fullLLMResponse,
            @"(?<!\])\w+\([^)]*\)(?!\[)",
            match => $"[b][color=#00ff00]{match.Value}[/color][/b]"
        );

        LogManager.UpdateLog("llmResponse", _fullLLMResponse);

        List<Command> commands = Parse(chunk);

        if (commands.Count > 0)
        {
            countRecognized++;
        }

        foreach (Command command in commands)
        {
            string prefix = objectInteractionVerbPrefixes.FirstOrDefault(
                prefix =>
                {
                    var result = command.Verb.Contains(prefix);
                    return result;
                },
                null
            );

            if (prefix != null)
            {
                string objectType = Regex.Replace(command.Verb, prefix, "").ToLower();

                var target = TargetResolution.NormalizeTargetString(command.Arguments[0]);

                AcknowledgedCommandsSet.Add(prefix + "/" + objectType + "/" + target);

                bus.EmitSignal(EventBus.SignalName.ObjectInteraction, prefix, objectType, target);

                return;
            }

            if (ghostActionVerbs.Contains(command.Verb))
            {
                if (command.Verb == "moveAsGhost" || command.Verb == "moveToAsGhost")
                {
                    var target = TargetResolution.NormalizeTargetString(command.Arguments[0]);

                    bus.EmitSignal(EventBus.SignalName.GhostAction, command.Verb, target);
                    return;
                }

                bus.EmitSignal(
                    EventBus.SignalName.GhostAction,
                    command.Verb,
                    command.Arguments.Aggregate("", (acc, arg) => acc + arg + " ")
                );

                bus.EmitSignal(
                    EventBus.SignalName.NotableEventOccurred,
                    $"Ghost action - {command.Verb}({command.Arguments.Aggregate("", (acc, arg) => acc + arg + " ")})"
                );

                return;
            }
        }
    }
}
