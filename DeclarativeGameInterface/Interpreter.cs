using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using FuzzySharp;
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

    private HashSet<string> InvalidCommandsSet = new();
    private List<string> RecentCommands = new();

    private bool GhostDepositedEvidence = false;
    private int CyclesSinceLastEvidenceDeposit = 0;

    private bool GhostHasChased = false;
    private int CyclesSinceLastChase = 0;

    private bool AnyCommandWasPerformed = false;

    private Node ghostData;

    private int Cycles = 0;

    private LLMInterface llmInterface;
    private NarrativeIntegrity Integrity;

    private EventBus Bus;

    public override void _Ready()
    {
        LogManager.UpdateLog("llmResponse", "");

        ghostData = GetTree().Root.GetNode<Node>("GhostData");

        player = (Node3D)GetTree().CurrentScene.FindChild("Player");
        ghost = (Node3D)GetTree().CurrentScene.FindChild("Ghost");
        llmInterface = (LLMInterface)GetTree().Root.GetNode("LLMInterface");
        Integrity = (NarrativeIntegrity)GetTree().Root.GetNode("NarrativeIntegrity");

        GD.Print("Initialized interpreter with values player: ", player, " ghost: ", ghost);

        AllVerbs.AddRange(objectInteractionVerbs);
        AllVerbs.AddRange(ghostActionVerbs);
        AllVerbs.AddRange(internalVerbs);
        AllVerbs.AddRange(playerEffectVerbs);

        Bus = EventBus.Get();

        Bus.LLMFirstResponseChunk += (chunk) =>
        {
            _fullLLMResponse = "";
            LogManager.UpdateLog("llmResponse", "");
        };

        Bus.LLMResponseChunk += (chunk) =>
        {
            Interpret(chunk);
        };

        Bus.ObjectInteractionAcknowledged += (verb, objectType, target) =>
        {
            if (InvalidCommandsSet.Contains(verb + "/" + objectType + "/" + target)) // This is a hack to prevent double logging
            {
                Bus.EmitSignal(
                    EventBus.SignalName.NotableEventOccurred,
                    $"Ghost interacted - {verb}{textInfo.ToTitleCase(objectType)}({target})"
                );

                AddToRecentCommands($"{verb}{textInfo.ToTitleCase(objectType)}({target})");
            }

            InvalidCommandsSet.Remove(verb + "/" + objectType + "/" + target);
        };

        Bus.LLMLastResponseChunk += (_chunk) =>
        {
            foreach (string acknowledgedCommand in InvalidCommandsSet)
            {
                var split = acknowledgedCommand.Split("/");
                var verb = split[0];
                var objectType = split[1];
                var target = split[2];

                Bus.EmitSignal(
                    EventBus.SignalName.SystemFeedback,
                    $"OBJECT DOESN'T EXIST: Trying to {verb} {objectType} in/at {target} FAILED because the object doesn't exist in {target}! Refer to the available objects per room in ROOM INFORMATION."
                );
            }

            var commandFrequencies = GetCommandFrequencies();

            if (commandFrequencies.Count > 0)
            {
                var mostFrequentCommand = commandFrequencies.Aggregate(
                    (l, r) => l.Value > r.Value ? l : r
                );

                if (mostFrequentCommand.Value > 2)
                {
                    Bus.EmitSignal(
                        EventBus.SignalName.SystemFeedback,
                        $"WARNING: You are using {mostFrequentCommand.Key} too often ({mostFrequentCommand.Value} times recently). You are becoming predictable. VARY your commands, utilize what is available to you!"
                    );

                    RecentCommands.RemoveAll(match => match.Contains(mostFrequentCommand.Key));
                }
            }

            if (Cycles > 0 && Cycles % 10 == 0) // Every 10 cycles
            {
                var unusedVerbs = new List<string>(objectInteractionVerbs);
                unusedVerbs.AddRange(ghostActionVerbs);

                foreach (string _ in RecentCommands)
                {
                    var command = _.Split("(")[0];

                    if (unusedVerbs.Contains(command))
                    {
                        unusedVerbs.Remove(command);
                    }
                }

                if (unusedVerbs.Count > 0)
                {
                    Bus.EmitSignal(
                        EventBus.SignalName.SystemFeedback,
                        $"WARNING: You have not used the following commands recently: {unusedVerbs.Aggregate("", (acc, verb) => acc + verb + ", ").Trim()}. DIVERSIFY your command usage for a more engaging experience."
                    );
                }
            }

            InvalidCommandsSet.Clear();

            if (!GhostDepositedEvidence)
            {
                CyclesSinceLastEvidenceDeposit++;
            }

            if (!GhostHasChased)
            {
                CyclesSinceLastChase++;
            }

            if (!AnyCommandWasPerformed)
            {
                Bus.EmitSignal(
                    EventBus.SignalName.SystemFeedback,
                    "ERROR: ⁉ AI director performed no commands! This is UNACCEPTABLE! PLEASE remember to invoke commands using the appropriate syntax to enact change in the game world!"
                );
            }

            if (CyclesSinceLastEvidenceDeposit > 20)
            {
                Bus.EmitSignal(
                    EventBus.SignalName.SystemFeedback,
                    "WARNING: ⚠ AI director has not deposited evidence in a while. This UNDERMINES PLAYER AGENCY! Please deposit evidence to progress the game."
                );
                CyclesSinceLastEvidenceDeposit = 0;
                GhostDepositedEvidence = false;
            }

            if (CyclesSinceLastChase > 10)
            {
                Bus.EmitSignal(
                    EventBus.SignalName.SystemFeedback,
                    "### ❗❗❗ AI director did not start ghost chase in a while. This MAKES THE GAME BORING! Use `chasePlayerAsGhost` to reinforce the horror element! ❗❗❗ ###"
                );
                CyclesSinceLastChase = 0;
                GhostHasChased = false;
            }

            AnyCommandWasPerformed = false;

            Cycles++;
        };
    }

    private void AddToRecentCommands(string fullCommand)
    {
        RecentCommands.Add(fullCommand);
        RecentCommands = RecentCommands.TakeLast(10).ToList();
    }

    private Dictionary<string, int> GetCommandFrequencies()
    {
        var commandCounts = new Dictionary<string, int>();

        foreach (string command in RecentCommands)
        {
            if (commandCounts.ContainsKey(command))
            {
                commandCounts[command]++;
            }
            else
            {
                commandCounts[command] = 1;
            }
        }

        return commandCounts;
    }

    private List<string> objectInteractionVerbPrefixes =
        new()
        {
            "flicker",
            "explode",
            "restore",
            "turnoff",
            "turnon",
            "playfreakymusicon",
            "stop",
            "open",
            "close",
            "unlock",
            "lock",
            "throw",
            "jolt",
            "shift",
        };

    private List<string> objectInteractionVerbs =
        new()
        {
            "turnofflights",
            "flickerlights",
            "explodelights",
            "restorelights",
            "turnonradios",
            "turnoffradios",
            "playfreakymusiconradios",
            "stopradios",
            "opendoors",
            "closedoors",
            "lockdoors",
            "unlockdoors",
            "shiftobjects",
            "joltobjects",
            "throwobjects",
        };

    private List<string> ghostActionVerbs =
        new()
        {
            "moveasghost",
            "movetoasghost",
            "chaseplayerasghost",
            "speakasghost",
            "appearasghost",
            "depositevidenceasghost",
            "emitsoundasghost",
            "emitsoundinroom",
        };

    private List<string> playerEffectVerbs = new() { "pullplayertoghost", "dimPlayerFlashlight" };

    private List<string> internalVerbs = new() { "amendsystemfeedback" };

    private List<string> AllVerbs = new();

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
                var verb = separatedString[0].ToLower();

                if (AllVerbs.Contains(verb) == false)
                {
                    // Attempt fuzzy matching
                    var bestMatch = AllVerbs.FindAll(v => Fuzz.PartialRatio(v, verb) > 80);

                    if (bestMatch.Count > 0)
                    {
                        GD.Print(
                            "Parser: command verb "
                                + verb
                                + " not found, but a viable best match is "
                                + bestMatch[0]
                        );
                        verb = bestMatch[0];
                    }
                    else
                    {
                        GD.Print(
                            "Parser: command verb "
                                + verb
                                + " not found, and no viable best match found."
                        );
                        Bus.EmitSignal(
                            EventBus.SignalName.SystemFeedback,
                            $"ERROR: Command {verb} does NOT exist. PLEASE refer to the system prompt available at your disposal.\nFurther errors could result in TERMINATION of the game."
                        );
                        continue;
                    }
                }

                var argumentString = separatedString[1].Substring(0, separatedString[1].Length - 1);
                var arguments = argumentString
                    .Split(",")
                    .Select(argument => argument.Trim().ToLower())
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

    public async void Interpret(string chunk)
    {
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
            countRecognized += commands.Count;
        }

        for (int i = 0; i < commands.Count; i++)
        {
            Command command = commands[i];

            GD.Print(
                "command is "
                    + command.Verb
                    + " with args "
                    + command.Arguments.Aggregate("", (acc, arg) => acc + arg + " ").Trim()
            );

            string objectInteractionPrefix = objectInteractionVerbPrefixes.FirstOrDefault(
                prefix =>
                {
                    var result = command.Verb.Contains(prefix);
                    return result;
                },
                null
            );

            if (internalVerbs.Contains(command.Verb))
            {
                if (command.Verb == "amendsystemfeedback")
                {
                    var keyword = new string(
                        command
                            .Arguments[0]
                            .Where((c) => char.IsLetterOrDigit(c) || c == ' ')
                            .ToArray()
                    );

                    Bus.EmitSignal(EventBus.SignalName.AmendSystemFeedback, keyword);
                    continue;
                }
            }

            if (objectInteractionPrefix != null)
            {
                AnyCommandWasPerformed = true;
                string objectType = Regex
                    .Replace(command.Verb, objectInteractionPrefix, "")
                    .ToLower();

                var target = TargetResolution.NormalizeTargetString(command.Arguments[0]);

                if (TargetResolution.IsValidTarget(target) == false)
                {
                    Bus.EmitSignal(
                        EventBus.SignalName.SystemFeedback,
                        $"TARGET DOESN'T EXIST: Trying to {objectInteractionPrefix} {objectType} in/at {target} FAILED. Because there is no such thing as \"{target}\"! Refer to the available rooms in ROOM INFORMATION."
                    );

                    continue;
                }

                InvalidCommandsSet.Add(objectInteractionPrefix + "/" + objectType + "/" + target);

                Bus.EmitSignal(
                    EventBus.SignalName.ObjectInteraction,
                    objectInteractionPrefix,
                    objectType,
                    target
                );

                continue;
            }

            if (ghostActionVerbs.Contains(command.Verb))
            {
                AnyCommandWasPerformed = true;
                if (command.Verb == "moveasghost" || command.Verb == "movetoasghost")
                {
                    var target = TargetResolution.NormalizeTargetString(command.Arguments[0]);

                    if (TargetResolution.IsValidTarget(target) == false)
                    {
                        Bus.EmitSignal(
                            EventBus.SignalName.SystemFeedback,
                            $"TARGET DOESN'T EXIST: Trying to {command.Verb} {target} FAILED. Because there is no such thing as \"{target}\"! Refer to the available rooms in ROOM INFORMATION."
                        );

                        GD.Print("isn't a valid target");

                        continue;
                    }

                    Bus.EmitSignal(EventBus.SignalName.GhostAction, command.Verb, target);
                    Bus.EmitSignal(
                        EventBus.SignalName.NotableEventOccurred,
                        $"Ghost action - {command.Verb}({command.Arguments.Aggregate("", (acc, arg) => acc + arg + " ").Trim()})"
                    );
                    AddToRecentCommands(
                        $"{command.Verb}({command.Arguments.Aggregate("", (acc, arg) => acc + arg + " ").Trim()})"
                    );

                    continue;
                }

                if (command.Verb == "depositevidenceasghost")
                {
                    Bus.EmitSignal(EventBus.SignalName.AmendSystemFeedback, "deposit evidence");
                    GhostDepositedEvidence = true;
                    CyclesSinceLastEvidenceDeposit = 0;
                }

                if (command.Verb == "chaseplayerasghost")
                {
                    if (player.GetNode("Locator").Get("Room").AsString() == "None")
                    {
                        Bus.EmitSignal(
                            EventBus.SignalName.SystemFeedback,
                            "ERROR: You tried to chase the player with `chasePlayerAsGhost`, but the player is currently outside. Try again when they're inside."
                        );
                        continue;
                    }

                    Bus.EmitSignal(EventBus.SignalName.AmendSystemFeedback, "chase");
                    GhostHasChased = true;
                    CyclesSinceLastChase = 0;

                    Bus.EmitSignal(
                        EventBus.SignalName.ObjectInteraction,
                        "lock",
                        "doors",
                        "entrance"
                    );
                }

                if (command.Verb == "speakasghost")
                {
                    var message = "";

                    for (int j = 0; j < command.Arguments.Count; j++)
                    {
                        message += command.Arguments[j];
                        if (j < command.Arguments.Count - 1)
                        {
                            message += ", ";
                        }
                    }

                    message = await Integrity.CheckIntegrityForAudio(message, "speakAsGhost");

                    message = new string(
                        message
                            .Where(
                                c =>
                                    char.IsLetterOrDigit(c)
                                    || c == ' '
                                    || c == ','
                                    || c == '.'
                                    || c == '!'
                                    || c == '?'
                                    || c == '\''
                            )
                            .ToArray()
                    );

                    message = message.Replace("-", "");
                    message = message.Replace("player", "");

                    command.Arguments = new() { message };
                }

                Bus.EmitSignal(
                    EventBus.SignalName.GhostAction,
                    command.Verb,
                    command.Arguments.Aggregate("", (acc, arg) => acc + arg + " ").Trim()
                );

                if (command.Verb == "speakasghost")
                {
                    Bus.EmitSignal(EventBus.SignalName.GhostTalked, command.Arguments[0]);
                }
                else
                {
                    Bus.EmitSignal(
                        EventBus.SignalName.NotableEventOccurred,
                        $"Ghost action - {command.Verb}({command.Arguments.Aggregate("", (acc, arg) => acc + arg + " ").Trim()})"
                    );
                }

                AddToRecentCommands(
                    $"{command.Verb}({command.Arguments.Aggregate("", (acc, arg) => acc + arg + " ").Trim()})"
                );

                continue;
            }

            if (playerEffectVerbs.Contains(command.Verb))
            {
                Bus.EmitSignal(
                    EventBus.SignalName.NotableEventOccurred,
                    $"Ghost meddled with player - {command.Verb} {command.Arguments.Aggregate("", (acc, arg) => acc + arg + " ").Trim()}"
                );

                Bus.EmitSignal(
                    EventBus.SignalName.PlayerEffect,
                    command.Verb,
                    command.Arguments.Aggregate("", (acc, arg) => acc + arg + " ").Trim()
                );
            }

            Bus.EmitSignal(
                EventBus.SignalName.SystemFeedback,
                $"ERROR: Command {command.Verb}({command.Arguments.Aggregate("", (acc, arg) => acc + arg + " ").Trim()}) does NOT exist. PLEASE refer to the previously stated commands available at your disposal.\nFurther errors could result in TERMINATION of the game."
            );
        }
    }
}
