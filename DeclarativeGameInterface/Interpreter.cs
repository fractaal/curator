using System;
using System.Collections.Generic;
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

    public override void _Ready()
    {
        LogManager.UpdateLog("llmResponse", "");

        player = (Node3D)GetTree().CurrentScene.FindChild("Player");
        ghost = (Node3D)GetTree().CurrentScene.FindChild("Ghost");

        GD.Print("Initialized interpreter with values player: ", player, " ghost: ", ghost);

        EventBus.Get().LLMFirstResponseChunk += (chunk) =>
        {
            GD.Print("reset llm response");
            _fullLLMResponse = "";
            LogManager.UpdateLog("llmResponse", "");
        };

        EventBus.Get().LLMResponseChunk += (chunk) =>
        {
            Interpret(chunk);
        };
    }

    private List<string> objectInteractionVerbPrefixes =
        new() { "flicker", "explode", "restore", "turnOff" };

    private List<string> ghostActionVerbs =
        new()
        {
            "move",
            "moveTo",
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

        var preMatches = Regex.Matches(accumulatedText, pattern);
        if (preMatches.Count() > 0)
        {
            accumulatedText = accumulatedText.Substring(preMatches[0].Index + preMatches[0].Length);
            accumulatedText = Regex.Replace(accumulatedText, pattern, "");
        }

        accumulatedText += chunk;

        var matches = Regex.Matches(accumulatedText, pattern);

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
            pattern,
            match => $"[b][color=#00ff00]{match.Value}[/color][/b]"
        );

        LogManager.UpdateLog("llmResponse", _fullLLMResponse);

        List<Command> commands = Parse(chunk);

        var newlineToSpaceString = Regex.Replace(accumulatedText, @"(\r\n|\n)", " ");
        var highlightedMatchesString = Regex.Replace(
            newlineToSpaceString,
            pattern,
            match => $"[b][color=#00ff00]{match.Value}[/color][/b]"
        );

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

                bus.EmitSignal(
                    EventBus.SignalName.ObjectInteraction,
                    prefix,
                    objectType,
                    command.Arguments[0]
                );

                bus.EmitSignal(
                    EventBus.SignalName.NotableEventOccurred,
                    $"AI interacted - {prefix} {objectType} - {command.Arguments[0]}"
                );

                return;
            }

            if (ghostActionVerbs.Contains(command.Verb))
            {
                bus.EmitSignal(
                    EventBus.SignalName.GhostAction,
                    command.Verb,
                    command.Arguments.Aggregate("", (acc, arg) => acc + arg + " ")
                );

                bus.EmitSignal(
                    EventBus.SignalName.NotableEventOccurred,
                    $"AI acted as the Ghost - {command.Verb} - {command.Arguments.Aggregate("", (acc, arg) => acc + arg + " ")}"
                );

                return;
            }
        }
    }
}
