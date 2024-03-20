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

    [Signal]
    public delegate void ObjectInteractionEventHandler(
        string verb,
        string objectType,
        string target
    );

    private void _ready()
    {
        player = (Node3D)GetTree().CurrentScene.FindChild("Player");
        ghost = (Node3D)GetTree().CurrentScene.FindChild("Ghost");

        GD.Print("Initialized interpreter with values player: ", player, " ghost: ", ghost);
    }

    private List<string> objectInteractionVerbPrefixes =
        new() { "flicker", "explode", "restore", "turnOff" };

    private string accumulatedText = "";

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

    private int countRecognized = 0;

    public void Interpret(string chunk)
    {
        List<Command> commands = Parse(chunk);

        var pattern = @"\w+\([^)]*\)";

        var newlineToSpaceString = Regex.Replace(accumulatedText, @"(\r\n|\n)", " ");
        var highlightedMatchesString = Regex.Replace(
            newlineToSpaceString,
            pattern,
            match => $"[b][color=#00ff00]{match.Value}[/color][/b]"
        );

        LogManager.UpdateLog("recognized" + countRecognized, highlightedMatchesString);

        if (commands.Count > 0)
        {
            countRecognized++;
        }

        foreach (Command command in commands)
        {
            var searchSpace = getObjectSearchSpace(command.Arguments);

            string prefix = null;

            try
            {
                prefix = objectInteractionVerbPrefixes.First(prefix =>
                {
                    var result = command.Verb.Contains(prefix);
                    GD.Print("Object interaction prefix detected (", command.Verb, ")");
                    return result;
                });
            }
            catch (Exception e)
            {
                GD.Print(command.Verb, " did not have a valid object interaction prefix");
            }

            if (prefix != null)
            {
                GD.Print("Was object interaction command!");

                foreach (Node3D node in searchSpace)
                {
                    node.Call(prefix); // Convention.
                }
            }
        }
    }

    private List<Node3D> getObjectSearchSpace(List<string> arguments)
    {
        int inKeywordIndex = arguments.IndexOf("in");
        string roomName = null;

        var lights = GetTree().GetNodesInGroup("lights").Cast<Node3D>().ToList();
        var tvs = GetTree().GetNodesInGroup("tvs").Cast<Node3D>().ToList();

        if (inKeywordIndex != -1)
        {
            roomName = arguments[inKeywordIndex + 1];
        }

        if (roomName == null)
        {
            List<Node3D> nodes = new();
            nodes.AddRange(lights);
            nodes.AddRange(tvs);
            return nodes;
        }

        var rooms = GetTree().GetNodesInGroup("rooms");

        Area3D selectedRoom = null;

        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i].Name.ToString().ToLower().Contains(roomName.ToLower()))
            {
                selectedRoom = (Area3D)rooms[i];
                break;
            }
        }

        if (selectedRoom == null)
        {
            GD.Print(roomName, " apparently does not exist. Defaulting to all");
            List<Node3D> nodes = new();

            nodes.AddRange(lights);
            nodes.AddRange(tvs);
            return nodes;
        }

        var roomNodes = selectedRoom.GetOverlappingBodies().ToList();

        List<Node3D> validNodes = new();

        foreach (Node3D roomNode in roomNodes)
        {
            Node currentNode = roomNode;

            while (currentNode.GetParent() != null)
            {
                if (currentNode.IsInGroup("lights") || currentNode.IsInGroup("tvs"))
                {
                    validNodes.Add((Node3D)currentNode);
                }
                currentNode = currentNode.GetParent();
            }
        }

        validNodes.ForEach(n => GD.Print(n.Name));

        return validNodes;
    }
}
